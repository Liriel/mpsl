#!/bin/bash
pushd ClientApp
npm run publish
popd
docker build -f Dockerfile.alpine-x64-slim -t peach.heisl.org/mpsl .
docker push peach.heisl.org/mpsl
#ssh core@192.168.0.248 "cd mpsl && ./updateContainer.sh"
