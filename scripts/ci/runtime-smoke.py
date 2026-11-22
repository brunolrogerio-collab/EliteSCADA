#!/usr/bin/env python3
"""Shared full Runtime smoke used by hosted CI and the local parity harness."""
from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from http.cookiejar import CookieJar
from pathlib import Path
from urllib.error import URLError
from urllib.request import HTTPCookieProcessor, Request, build_opener

root = Path(__file__).resolve().parents[2]
base_url = os.environ.get("ELITESCADA_CI_API_URL", "http://127.0.0.1:5080")
artifacts = Path(os.environ.get("ELITESCADA_CI_ARTIFACTS", root / "ci" / "local" / "artifacts" / "runtime-smoke"))
artifacts.mkdir(parents=True, exist_ok=True)
log = artifacts / "scada-api.log"
opener = build_opener(HTTPCookieProcessor(CookieJar()))

def request(path: str, method: str = "GET", payload: dict | None = None):
    data = json.dumps(payload).encode() if payload is not None else None
    req = Request(base_url + path, data=data, method=method)
    if data is not None:
        req.add_header("content-type", "application/json")
    with opener.open(req, timeout=10) as response:
        body = response.read().decode()
    artifact_name = (path.strip("/").replace("/", "_").replace("?", "_").replace("&", "_").replace("=", "-") or "root")
    (artifacts / artifact_name).with_suffix(".json").write_text(body, encoding="utf-8")
    return json.loads(body)

def wait_for(path: str, predicate, label: str):
    last = None
    for _ in range(30):
        try:
            last = request(path)
            if predicate(last):
                return last
        except (URLError, OSError, ValueError) as error:
            last = str(error)
        time.sleep(1)
    raise RuntimeError(f"{label} was not ready: {last}")

def required(condition: bool, message: str):
    if not condition:
        raise AssertionError(message)

environment = os.environ.copy()
environment.setdefault("ASPNETCORE_URLS", "http://127.0.0.1:5080")
environment.setdefault("ConnectionStrings__EliteScada", "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=postgres;Pooling=false")
environment.setdefault("Historian__Provider", "timescaledb")
environment.setdefault("HistoricalQuery__Enabled", "true")
environment.setdefault("HistoricalQuery__CursorKeyBase64", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")
# Start with the ordinary Runtime baseline. The authority-owned first-project
# contract is proven by a fresh secure process below.
environment["Authentication__Enabled"] = "false"
environment["Authentication__Local__Enabled"] = "false"
environment["Authentication__Local__SecureCookie"] = "false"
environment["Authentication__Jwt__Issuer"] = "elitescada-ci"
environment["Authentication__Jwt__Audience"] = "elitescada-ci"
environment["Authentication__Jwt__SigningKey"] = "elitescada-ci-signing-key-at-least-32-bytes"

def start_api(configuration: dict[str, str]):
    output = log.open("a", encoding="utf-8")
    return subprocess.Popen(
        ["dotnet", "run", "--project", "src/Scada.Api/Scada.Api.csproj", "--no-build", "--configuration", "Release", "--no-launch-profile",
         "--", "--Authentication:Enabled=" + configuration["Authentication__Enabled"],
         "--Authentication:Local:Enabled=" + configuration["Authentication__Local__Enabled"],
         "--Authentication:Local:SecureCookie=" + configuration["Authentication__Local__SecureCookie"],
         "--Authentication:Jwt:Issuer=" + configuration["Authentication__Jwt__Issuer"],
         "--Authentication:Jwt:Audience=" + configuration["Authentication__Jwt__Audience"],
         "--Authentication:Jwt:SigningKey=" + configuration["Authentication__Jwt__SigningKey"]],
        cwd=root, env=configuration, stdout=output, stderr=subprocess.STDOUT)

log.write_text("", encoding="utf-8")
api = start_api(environment)
try:
    health = wait_for("/health", lambda value: value == {"status": "ok", "service": "scada-api"}, "API health")
    required(health == {"status": "ok", "service": "scada-api"}, "unexpected health payload")
    diagnostics = wait_for("/api/diagnostics/runtime", lambda value: value["historian"]["provider"] == "timescaledb" and value["historian"]["writtenSamples"] >= 1, "TimescaleDB historian")
    tags = request("/api/tags")
    required(len(tags) >= 7, "Runtime did not expose the expected Demo TAGs")
    tank = next(tag for tag in tags if tag["path"] == "Demo.Tank01.Level")
    required(tank["id"] == "10000000-0000-0000-0000-000000000001", "unexpected tank identity")
    history = request(f"/api/history/{tank['id']}?limit=100")
    required(len(history) >= 1 and all(sample["quality"] == 0 for sample in history), "historian samples are invalid")
    query = request("/api/historical/query", "POST", {"datasetKey": "historian.samples", "timeRange": {"kind": "relative", "durationSeconds": 300, "anchor": "now"}, "page": {"limit": 5}})
    required(query["version"] == 1 and query["datasetKey"] == "historian.samples" and len(query["rows"]) >= 1, "historical query failed")
    required({column["field"] for column in query["columns"]} >= {"tag.id", "tag.path", "timestamp", "value", "quality"}, "historical query columns changed")
    roles = request("/api/engineering/security-roles")
    developer = next(role for role in roles if role["key"] == "developer")
    required({role["key"] for role in roles} == {"developer"}, "unexpected Authority roles")
    required(set(developer["grants"][index]["capability"] for index in range(len(developer["grants"]))) == {"view", "tagRead", "commandExecute", "processValueWrite", "alarmAcknowledge", "alarmShelve", "trendUse", "trendSave", "engineeringView", "engineeringModify", "userRoleAdmin", "systemAdmin"}, "unexpected developer capability set")
    required(request("/api/engineering/commands") == [], "fresh workspace commands must be empty")
    status = request("/api/engineering/persistence/status")
    required(status["enabled"] is True and status["provider"] == "postgresql", "persistence is not PostgreSQL")
    engineering = request("/api/engineering/export/json")
    api.terminate()
    api.wait(timeout=10)
    environment["Authentication__Enabled"] = "true"
    environment["Authentication__Local__Enabled"] = "true"
    api = start_api(environment)
    wait_for("/health", lambda value: value == {"status": "ok", "service": "scada-api"}, "secure API health")
    auth = request("/api/auth/config")
    required(auth["authenticationEnabled"] and auth["localLoginEnabled"] and auth["initialAdministratorRequired"], "secure first-run is not available")
    administrator = request("/api/auth/bootstrap", "POST", {"username": "ci-admin", "displayName": "CI Administrator", "password": "ci-admin-password"})
    required(administrator["username"] == "ci-admin", "initial administrator bootstrap failed")
    session = request("/api/auth/local-session")
    required(session["authenticated"] is True and session["username"] == "ci-admin", "initial administrator session was not retained")
    first = request("/api/engineering/persistence/projects/first", "POST", {"projectKey": "ci-demo", "projectName": "CI Demo"})
    revision = first["revision"]
    required(revision["revision"] >= 1 and revision["basedOnRevision"] is None, "first project is not a root revision")
    workspace = first["workspace"]
    required(workspace["projectKey"] == "ci-demo" and workspace["baseRevision"] == revision["revision"] and workspace["isDirty"] is False, "first project workspace was not accepted")
    required(workspace["securityRoleCount"] == 1 and workspace["dynamoCount"] == 8 and workspace["commandCount"] == 0, "canonical built-in first-project bootstrap changed")
    lifecycle = request("/api/engineering/persistence/ci-demo/lifecycle")
    required(lifecycle["status"] == 1 and lifecycle["workingRevision"] == revision["revision"] and lifecycle["publishedRevision"] is None, "first project lifecycle is not Draft")
    revisions = request("/api/engineering/persistence/ci-demo/revisions")
    required(revisions[0]["projectKey"] == "ci-demo" and revisions[0]["engineeringSchemaVersion"] == engineering["schemaVersion"] and revisions[0]["basedOnRevision"] is None, "persisted root revision is invalid")
    preview = request("/api/engineering/persistence/ci-demo/latest/preview", "POST")
    required(preview["preview"]["canApply"] is True and preview["revision"]["projectKey"] == "ci-demo", "latest preview failed")
    published = request(f"/api/engineering/persistence/ci-demo/revisions/{revision['revision']}/publish", "POST", {"publishedBy": "ci-parity"})
    required(published["publication"]["publishedRevision"] == revision["revision"] and published["lifecycle"]["status"] == 2, "publish failed")
    second = request("/api/engineering/persistence/ci-demo/save", "POST", {"projectName": "CI Demo", "savedBy": "ci-parity-after-publish"})
    required(second["revision"] > revision["revision"] and second["basedOnRevision"] == revision["revision"], "ordinary derived save failed")
    pending = request("/api/engineering/persistence/ci-demo/lifecycle")
    required(pending["status"] == 3 and pending["workingRevision"] > pending["publishedRevision"], "post-publish lifecycle is not Pending")
    print(json.dumps({"health": health, "diagnostics": diagnostics, "firstProjectRevision": revision["revision"], "derivedRevision": second["revision"]}))
except Exception:
    if log.exists():
        print("=== scada-api.log ===", file=sys.stderr)
        print(log.read_text(encoding="utf-8", errors="replace"), file=sys.stderr)
    raise
finally:
    api.terminate()
    try:
        api.wait(timeout=10)
    except subprocess.TimeoutExpired:
        api.kill()
