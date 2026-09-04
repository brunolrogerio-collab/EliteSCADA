import ast
import json
import sys

MAX_STEPS = 10000


class ScriptError(Exception):
    pass


class ReturnSignal(Exception):
    def __init__(self, value):
        self.value = value


class SafeInterpreter:
    def __init__(self, source, values, event, server_memory_tag_ids):
        self.tree = ast.parse(source, mode="exec")
        self._validate_module()
        self.values = {str(key).lower(): value for key, value in values.items()}
        self.server_memory_tag_ids = {
            str(key).lower() for key in (server_memory_tag_ids or [])
        }
        self.event = event or {}
        self.writes = []
        self.functions = {
            node.name: node for node in self.tree.body if isinstance(node, ast.FunctionDef)
        }
        self.steps = 0

    def _validate_module(self):
        for node in self.tree.body:
            if not isinstance(node, ast.FunctionDef):
                raise ScriptError(
                    "Server Script top level may contain function declarations only."
                )
            if node.decorator_list:
                raise ScriptError("Function decorators are not supported.")
            if node.args.defaults or node.args.kw_defaults:
                raise ScriptError("Default function arguments are not supported.")

    def tick(self):
        self.steps += 1
        if self.steps > MAX_STEPS:
            raise ScriptError("Server Script exceeded the deterministic instruction budget.")

    def run(self, handler_name):
        function = self.functions.get(handler_name)
        if function is None:
            raise ScriptError("Configured Python handler was not found.")
        args = [self.event] if function.args.args else []
        return self.call_function(function, args)

    def call_function(self, function, args):
        if (
            function.args.vararg
            or function.args.kwarg
            or function.args.kwonlyargs
            or function.args.posonlyargs
        ):
            raise ScriptError(
                "Variadic, positional-only and keyword-only arguments are not supported."
            )
        if len(args) != len(function.args.args):
            raise ScriptError(
                "Handler argument count does not match the runtime event contract."
            )
        env = {arg.arg: value for arg, value in zip(function.args.args, args)}
        try:
            self.exec_statements(function.body, env)
        except ReturnSignal as signal:
            return signal.value
        return None

    def exec_statements(self, statements, env):
        for statement in statements:
            self.tick()
            if isinstance(statement, ast.Assign):
                if len(statement.targets) != 1 or not isinstance(
                    statement.targets[0], ast.Name
                ):
                    raise ScriptError("Only simple local assignments are supported.")
                env[statement.targets[0].id] = self.eval_expr(statement.value, env)
            elif isinstance(statement, ast.AugAssign):
                if not isinstance(statement.target, ast.Name):
                    raise ScriptError(
                        "Only simple augmented assignments are supported."
                    )
                current = env.get(statement.target.id)
                env[statement.target.id] = self.apply_binary(
                    statement.op,
                    current,
                    self.eval_expr(statement.value, env),
                )
            elif isinstance(statement, ast.Expr):
                self.eval_expr(statement.value, env)
            elif isinstance(statement, ast.If):
                branch = (
                    statement.body
                    if self.eval_expr(statement.test, env)
                    else statement.orelse
                )
                self.exec_statements(branch, env)
            elif isinstance(statement, ast.Return):
                raise ReturnSignal(
                    None
                    if statement.value is None
                    else self.eval_expr(statement.value, env)
                )
            elif isinstance(statement, ast.Pass):
                continue
            else:
                raise ScriptError(
                    "Python statement is outside the deterministic Server Script subset."
                )

    def eval_expr(self, node, env):
        self.tick()
        if isinstance(node, ast.Constant):
            return node.value
        if isinstance(node, ast.Name):
            if node.id in env:
                return env[node.id]
            if node.id == "True":
                return True
            if node.id == "False":
                return False
            if node.id == "None":
                return None
            raise ScriptError("Unknown local name in Server Script.")
        if isinstance(node, ast.Dict):
            return {
                self.eval_expr(key, env): self.eval_expr(value, env)
                for key, value in zip(node.keys, node.values)
            }
        if isinstance(node, ast.List):
            return [self.eval_expr(item, env) for item in node.elts]
        if isinstance(node, ast.Tuple):
            return tuple(self.eval_expr(item, env) for item in node.elts)
        if isinstance(node, ast.Subscript):
            container = self.eval_expr(node.value, env)
            key = self.eval_expr(node.slice, env)
            return container[key]
        if isinstance(node, ast.UnaryOp):
            value = self.eval_expr(node.operand, env)
            if isinstance(node.op, ast.USub):
                return -value
            if isinstance(node.op, ast.UAdd):
                return +value
            if isinstance(node.op, ast.Not):
                return not value
            raise ScriptError("Unsupported unary operator.")
        if isinstance(node, ast.BinOp):
            return self.apply_binary(
                node.op,
                self.eval_expr(node.left, env),
                self.eval_expr(node.right, env),
            )
        if isinstance(node, ast.BoolOp):
            values = [self.eval_expr(item, env) for item in node.values]
            return all(values) if isinstance(node.op, ast.And) else any(values)
        if isinstance(node, ast.Compare):
            left = self.eval_expr(node.left, env)
            for operator, comparator in zip(node.ops, node.comparators):
                right = self.eval_expr(comparator, env)
                if not self.apply_compare(operator, left, right):
                    return False
                left = right
            return True
        if isinstance(node, ast.IfExp):
            return self.eval_expr(
                node.body if self.eval_expr(node.test, env) else node.orelse,
                env,
            )
        if isinstance(node, ast.Call):
            if not isinstance(node.func, ast.Name) or node.keywords:
                raise ScriptError(
                    "Only approved positional function calls are supported."
                )
            args = [self.eval_expr(arg, env) for arg in node.args]
            return self.call(node.func.id, args)
        raise ScriptError(
            "Python expression is outside the deterministic Server Script subset."
        )

    def call(self, name, args):
        if name in self.functions:
            return self.call_function(self.functions[name], args)

        if name == "read_tag":
            key = self._require_tag_argument(args, "TAG read")
            return self.values[key]

        if name == "read_server_memory":
            key = self._require_tag_argument(args, "Server Memory read")
            self._require_server_memory_capability(key)
            return self.values[key]

        if name == "write_tag":
            key, value = self._require_write_arguments(args, "TAG write")
            self.values[key] = value
            self.writes.append(
                {"tagId": key, "value": value, "serverMemoryOnly": False}
            )
            return None

        if name == "write_server_memory":
            key, value = self._require_write_arguments(args, "Server Memory write")
            self._require_server_memory_capability(key)
            self.values[key] = value
            self.writes.append(
                {"tagId": key, "value": value, "serverMemoryOnly": True}
            )
            return None

        builtins = {
            "min": min,
            "max": max,
            "abs": abs,
            "round": round,
            "int": int,
            "float": float,
            "bool": bool,
            "str": str,
            "len": len,
        }
        function = builtins.get(name)
        if function is None:
            raise ScriptError("Function call is not part of the Server Script API surface.")
        return function(*args)

    def _require_tag_argument(self, args, operation):
        if len(args) != 1:
            raise ScriptError(f"{operation} requires one stable TAG ID.")
        key = str(args[0]).lower()
        if key not in self.values:
            raise ScriptError("TAG is not an active declared dependency.")
        return key

    def _require_write_arguments(self, args, operation):
        if len(args) != 2:
            raise ScriptError(f"{operation} requires stable TAG ID and value.")
        key = str(args[0]).lower()
        if key not in self.values:
            raise ScriptError("TAG is not an active declared dependency.")
        return key, args[1]

    def _require_server_memory_capability(self, key):
        if key not in self.server_memory_tag_ids:
            raise ScriptError(
                "Server Memory API requires an explicit ServerMemoryTag dependency."
            )

    @staticmethod
    def apply_binary(operator, left, right):
        if isinstance(operator, ast.Add):
            return left + right
        if isinstance(operator, ast.Sub):
            return left - right
        if isinstance(operator, ast.Mult):
            return left * right
        if isinstance(operator, ast.Div):
            return left / right
        if isinstance(operator, ast.FloorDiv):
            return left // right
        if isinstance(operator, ast.Mod):
            return left % right
        if isinstance(operator, ast.Pow):
            return left ** right
        raise ScriptError("Unsupported binary operator.")

    @staticmethod
    def apply_compare(operator, left, right):
        if isinstance(operator, ast.Eq):
            return left == right
        if isinstance(operator, ast.NotEq):
            return left != right
        if isinstance(operator, ast.Lt):
            return left < right
        if isinstance(operator, ast.LtE):
            return left <= right
        if isinstance(operator, ast.Gt):
            return left > right
        if isinstance(operator, ast.GtE):
            return left >= right
        if isinstance(operator, ast.Is):
            return left is right
        if isinstance(operator, ast.IsNot):
            return left is not right
        raise ScriptError("Unsupported comparison operator.")


def main():
    try:
        payload = json.load(sys.stdin)
        interpreter = SafeInterpreter(
            payload.get("source", ""),
            payload.get("values") or {},
            payload.get("event") or {},
            payload.get("serverMemoryTagIds") or [],
        )
        interpreter.run(payload.get("handler", ""))
        result = {"succeeded": True, "error": None, "writes": interpreter.writes}
    except (
        ScriptError,
        SyntaxError,
        TypeError,
        ValueError,
        KeyError,
        IndexError,
        ZeroDivisionError,
        OverflowError,
    ) as error:
        result = {"succeeded": False, "error": str(error)[:240], "writes": []}
    print(json.dumps(result, separators=(",", ":")))


if __name__ == "__main__":
    main()
