"""Read-only postflight using the existing SystemAdmin-gated product endpoint."""
import argparse
import json
import os
import time
from urllib.parse import urlsplit
from urllib.request import Request, build_opener, ProxyHandler, HTTPRedirectHandler
from urllib.error import HTTPError


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, message, headers, new_url):
        return None


def verify(origin, bearer, timeout=60):
    uri=urlsplit(origin)
    if uri.scheme not in ('http','https') or uri.hostname not in ('127.0.0.1','::1','localhost') or uri.username or uri.password or uri.query or uri.fragment or uri.path not in ('','/'):
        raise ValueError('A local product origin is required')
    if not bearer or bearer.strip() != bearer or any(character.isspace() for character in bearer):
        raise ValueError('An existing temporary administrator session credential is required')
    opener=build_opener(ProxyHandler({}),NoRedirect())
    request=Request(origin.rstrip('/')+'/api/installation/database-readiness',headers={'Authorization':'Bearer '+bearer,'Accept':'application/json'})
    deadline=time.monotonic()+timeout
    while True:
        try:
            with opener.open(request,timeout=2) as response:
                body=response.read(4097)
                if len(body)>4096: raise RuntimeError('Unexpected readiness response')
                result=json.loads(body)
                if not isinstance(result,dict) or any(type(result.get(key)) is not bool for key in ('ready','restartRequired','recoveryRequired')):
                    raise RuntimeError('Unexpected readiness response')
                if result['ready'] and not result['restartRequired'] and not result['recoveryRequired']:
                    return 'Authenticated database readiness verified'
        except HTTPError as error:
            status=error.code; error.close()
            if status in (401,403): raise RuntimeError('Administrator session was rejected; no readiness acceptance') from None
            if status != 503: raise RuntimeError('Unexpected readiness HTTP status') from None
        except OSError:
            pass
        except (ValueError,UnicodeError):
            raise RuntimeError('Unexpected readiness response') from None
        if time.monotonic() >= deadline:
            raise RuntimeError('Authenticated readiness timed out')
        time.sleep(1)


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--origin',required=True)
    args=parser.parse_args()
    try:
        # Credential is inherited transiently, never a command argument/file/report.
        print(verify(args.origin,os.environ.get('SCADIVO_INSTALLATION_BEARER','')))
    except Exception:
        raise SystemExit('Authenticated readiness was not verified. Check the session and product status.')
