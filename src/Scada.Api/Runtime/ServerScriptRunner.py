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
    def __init__(self, source, values, event):
        self.tree = ast.parse(source, mode="exec")
        self.values = {str(key).lower(): value for key, value in values.items()}
        self.event = event or {}
        self.writes = []
        self.functions = {
            node.name: node for node in self.tree.body if isinstance(node, ast.FunctionDef)
        }
        self.steps = 0

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
        if function.args.vararg or function.args.kwarg or function.args.kwonlyargs:
            raise ScriptError("Variadic and keyword-only arguments are not supported.")
        if len(args) != len(function.args.args):
            raise ScriptError("Handler argument count does not match the runtime event contract.")
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
                if len(statement.targets) != 1 or not isinstance(statement.targets[0], ast.Name):
                    raise ScriptError("Only simple local assignments are supported.")
                env[statement.targets[0].id] = self.eval_expr(statement.value, env)
            elif isinstance(statement, ast.AugAssign):
                if not isinstance(statement.target, ast.Name):
                    raise ScriptError("Only simple augmented assignments are supported.")
                current = env.get(statement.target.id)
                env[statement.target.id] = self.apply_binary(statement.op, current, self.eval_expr(statement.value, env))
            elif isinstance(statement, ast.Expr):
                self.eval_expr(statement.value, env)
            elif isinstance(statement, ast.If):
                branch = statement.body if self.eval_expr(statement.test, env) else statement.orelse
                self.exec_statements(branch, env)
            elif isinstance(statement, ast.Return):
                raise ReturnSignal(None if statement.value is None else self.eval_expr(statement.value, env))
            elif isinstance(statement, ast.Pass):
                continue
            else:
                raise ScriptError("Python statement is outside the deterministic Server Script subset.")

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
            return {self.eval_expr(key, env): self.eval_expr(value, env) for key, value in zip(node.keys, node.values)}
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
            return self.apply_binary(node.op, self.eval_expr(node.left, env), self.eval_expr(node.right, env))
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
            return self.eval_expr(node.body if self.eval_expr(node.test, env) else node.orelse, env)
        if isinstance(node, ast.Call):
            if not isinstance(node.func, ast.Name) or node.keywords:
                raise ScriptError("Only approved positional function calls are supported.")
            args = [self.eval_expr(arg, env) for arg in node.args]
            return self.call(node.func.id, args, env)
        raise ScriptError("Python expression is outside the deterministic Server Script subset.")

    def call(self, name, args, env):
        if name in self.functions:
            return self.call_function(self.functions[name], args)
        if name == "read_tag" or name == "read_server_memory":
            if len(args) != 1:
                raise ScriptError("TAG read requires one stable TAG ID.")
            key = str(args[0]).lower()
            if key not in self.values:
                raise ScriptError("TAG is not an active declared dependency.")
            return self.values[key]
        if name == "write_tag" or name == "write_server_memory":
            if len(args) != 2:
                raise ScriptError("TAG write requires stable TAG ID and value.")
            key = str(args[0]).lower()
            if key not in self.values:
                raise ScriptError("TAG is not an active declared dependency.")
            self.values[key] = args[1]
            self.writes.append({
                "tagId": key,
                "value": args[1],
                "serverMemoryOnly": name == "write_server_memory",
            })
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
        )
        interpreter.run(payload.get("handler", ""))
        result = {"succeeded": True, "error": None, "writes": interpreter.writes}
    except (ScriptError, SyntaxError, TypeError, ValueError, KeyError, ZeroDivisionError, OverflowError) as error:
        result = {"succeeded": False, "error": str(error)[:240], "writes": []}
    print(json.dumps(result, separators=(",", ":")))


if __name__ == "__main__":
    main()
