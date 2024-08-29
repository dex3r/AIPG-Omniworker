#!/bin/bash
echo "Enter the version number:"
read version

docker tag aipg-dotnet-worker:latest dex3r/aipg-omniworker:$version
docker tag aipg-dotnet-worker:latest dex3r/aipg-omniworker:latest

docker push dex3r/aipg-omniworker:$version
docker push dex3r/aipg-omniworker:latest

echo "Done."
read 