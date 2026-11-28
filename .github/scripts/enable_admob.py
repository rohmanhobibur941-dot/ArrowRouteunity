"""Adds the Google Mobile Ads Unity plugin (from OpenUPM) to the project before a build.

Run by the workflow when you start it manually with "admob" ticked. It edits Packages/manifest.json and adds the
ARROW_ADMOB define to Assets/csc.rsp. Change the two versions below if OpenUPM has newer ones
(see https://openupm.com/packages/com.google.ads.mobile/).
"""
import json
import os

ADMOB_VERSION = os.environ.get("ADMOB_PLUGIN_VERSION", "9.4.0")
EDM_VERSION = os.environ.get("EDM_VERSION", "1.2.186")

with open("Packages/manifest.json") as f:
    manifest = json.load(f)

manifest["scopedRegistries"] = [{
    "name": "OpenUPM",
    "url": "https://package.openupm.com",
    "scopes": ["com.google.ads.mobile", "com.google.external-dependency-manager"],
}]
manifest["dependencies"]["com.google.ads.mobile"] = ADMOB_VERSION
manifest["dependencies"]["com.google.external-dependency-manager"] = EDM_VERSION

with open("Packages/manifest.json", "w") as f:
    json.dump(manifest, f, indent=2)

rsp = open("Assets/csc.rsp").read().splitlines() if os.path.exists("Assets/csc.rsp") else []
if "-define:ARROW_ADMOB" not in rsp:
    rsp.append("-define:ARROW_ADMOB")
with open("Assets/csc.rsp", "w") as f:
    f.write("\n".join(rsp) + "\n")

print("AdMob plugin", ADMOB_VERSION, "and EDM", EDM_VERSION, "added")
