This repository allows you to set up a AIPG Worker.

# Installing

## Windows

1. Install Docker if not installer already https://www.docker.com/products/docker-desktop/
1. Run this command: `(docker rm -f aipg-omniworker || ver > nul) && docker run -d -p 7870:8080 --gpus "all" --shm-size 8g --name aipg-omniworker dex3r/aipg-omniworker`
1. Go to http://localhost:7870/ and follow the instructions

## Linux

1. Install Docker if not installer already https://www.docker.com/products/docker-desktop/
1. Run this command: `sudo docker rm -f aipg-omniworker ; sudo docker run -d -p 7870:8080 --gpus "all" --shm-size 8g --name aipg-omniworker dex3r/aipg-omniworker`
1. Go to http://localhost:7870/ and follow the instructions

# Development

Use this command to quickly build from source and run the image. Output will be logged to console and the container will be stopped and removed after the command is stopped (e.g. CTRL+C). Don't forget to fill the missing variables `YOUR_GRID_KEY_HERE` and `YOUR_WORKER_NAME_HERE`.

```docker build -t aipg-dotnet-worker . && docker run --rm --mount source=aipg-omniworker-volume,target=/persistent -p 7870:8080 --gpus "all" --shm-size 8g --env GRID_API_KEY=YOUR_GRID_KEY_HERE --env WORKER_NAME=YOUR_WORKER_NAME_HERE --name aipg-omniworker aipg-dotnet-worker```
