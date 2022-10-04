#!/bin/bash
pushd ClientApp
npm run publish
popd
docker build -f Dockerfile.alpine-x64-slim -t liriel/mpsl .
docker push liriel/mpsl
