"""Stable host-only key provisioning. Existing keys are never rotated on restart."""
import base64
import os
import secrets

key_path = os.environ.get("ProtectedMaterial__Store__ProtectionKeyFile")
if key_path and not os.environ.get("ELITESCADA_PROTECTED_MATERIAL_KEY"):
    os.makedirs(os.path.dirname(key_path), mode=0o700, exist_ok=True)
    try:
        descriptor = os.open(key_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError:
        pass
    else:
        with os.fdopen(descriptor, "wb") as output:
            output.write(base64.b64encode(secrets.token_bytes(32)))
            output.flush()
            os.fsync(output.fileno())
os.execvp("dotnet", ["dotnet", "Scada.Api.dll"])
