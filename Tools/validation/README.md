# Harbor import verification

Create the flat, hash-bound contract from the preserved Blender manifest and the pre-expansion gate asset baseline:

```bash
python3 Tools/validation/produce_harbor_import_contract.py
```

Then run the actual Unity asset checks through the serialized wrapper:

```bash
Tools/unity.sh harbor-import-verification -nographics \
  -executeMethod Parasite.EditorTools.HarborArtVerification.VerifyBatch \
  -harborArtContract "$PWD/Logs/harbor-art/import-contract.json"
```

The verifier writes a unique JSON report under `Logs/harbor-art` and exits 0 or 1. It checks imported geometry metadata,
transformed bounds, static-model policy, texture references and source integrity. It does not render or generate scenes;
successful results do not establish gameplay, native-player appearance or performance.

The authored manifest and original gate baseline are pinned deliberately. Changed authoring requires a reviewed new contract,
not silent replacement of the old expected geometry or source hashes.
