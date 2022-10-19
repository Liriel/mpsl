#!/bin/bash
pushd ClientApp
npm run publish
popd
docker build -f Dockerfile.alpine-x64-slim -t peach.heisl.org/mpsl-amd64 .
docker push peach.heisl.org/mpsl-amd64
docker build -f Dockerfile.bullseye-slim-arm64v8 -t peach.heisl.org/mpsl-arm64 .
docker push peach.heisl.org/mpsl-arm64
docker manifest create peach.heisl.org/mpsl --amend peach.heisl.org/mpsl-amd64 --amend peach.heisl.org/mpsl-arm64
docker manifest push --purge peach.heisl.org/mpsl
ssh core@192.168.0.248 "cd mpsl && ./updateContainer.sh"
