#!/usr/bin/env python3
"""Read-only, sanitized deployment diagnostics. Does not print setting or secret values."""
import json
import subprocess
import urllib.request
BASE='https://management.azure.com/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-casa-example/providers/Microsoft.Web/sites/identity-api-example'
def az(*args):
 r=subprocess.run(['az',*args,'--output','json'],capture_output=True,text=True)
 if r.returncode: print('Azure diagnostic failed',args[:2]); return None
 return json.loads(r.stdout)
def walk(value,path=''):
 if isinstance(value,dict):
  for key,item in value.items():
   if key.lower() in ['status','error','errorcode','vaultname','secretname'] and isinstance(item,(str,int,bool)): print(path+'.'+key, item)
   elif isinstance(item,(dict,list)): walk(item,path+'.'+key)
 elif isinstance(value,list):
  for index,item in enumerate(value): walk(item,path+f'[{index}]')
for suffix in ['/config/configreferences/appsettings?api-version=2022-03-01']:
 result=az('rest','--method','get','--url',BASE+suffix)
 if result is not None: print('Reference response keys:',list(result) if isinstance(result,dict) else type(result).__name__);walk(result)
app=az('webapp','show','--resource-group','rg-casa-example','--name','identity-api-example')
if app:
 print('Current outbound addresses',app.get('outboundIpAddresses'))
rules=az('sql','server','firewall-rule','list','--resource-group','rg-casa-example','--server','sql-casa-example')
if rules: print('SQL allowed addresses',[(x['name'],x['startIpAddress'],x['endIpAddress']) for x in rules])
for route in ['/health','/ready','/api/v1/identity/.well-known/jwks.json']:
 try:
  with urllib.request.urlopen('https://identity-api.example.com'+route,timeout=15) as response: print(route,response.status)
 except Exception as error:
  print(route,type(error).__name__,getattr(error,'code',None))
  if route == '/ready' and hasattr(error,'read'):
   try: print('Dependency diagnostic code',json.loads(error.read()).get('dependencyErrorCode'))
   except (ValueError,TypeError): pass
