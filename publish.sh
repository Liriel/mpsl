#!/bin/bash
# The Angular client is built inside each image (node stage), so there is
# nothing to build on the dev machine first.

# amd64
docker build -f Dockerfile.x64 -t peach.heisl.org/mpsl-amd64 .
docker push peach.heisl.org/mpsl-amd64

# arm64
docker build -f Dockerfile.arm64v8 -t peach.heisl.org/mpsl-arm64 .
docker push peach.heisl.org/mpsl-arm64

# arm32
docker build -f Dockerfile.arm32v7 -t peach.heisl.org/mpsl-arm32 .
docker push peach.heisl.org/mpsl-arm32

# manifest
docker manifest create peach.heisl.org/mpsl --amend peach.heisl.org/mpsl-amd64 --amend peach.heisl.org/mpsl-arm64 --amend peach.heisl.org/mpsl-arm32
docker manifest push --purge peach.heisl.org/mpsl
# ssh core@192.168.0.248 "cd mpsl && ./updateContainer.sh"
