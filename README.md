This repository allows you to set up an AIPG Worker.

# Installing

## Windows

1. Install Docker if not installed already https://www.docker.com/products/docker-desktop/
2. Install CUDA Toolkit if not installed already https://developer.nvidia.com/cuda-downloads?target_os=Windows&target_arch=x86_64
1. Run this command: `(docker rm -f aipg-omniworker || ver > nul) && docker run -d -p 7870:8080 --gpus "all" --shm-size 8g --mount source=aipg-omniworker-volume,target=/persistent --name aipg-omniworker dex3r/aipg-omniworker`
1. Go to http://localhost:7870/ and follow the instructions

## Linux

1. Install Docker if not installed already https://www.docker.com/products/docker-desktop/
1. Install NVIDIA Container Toolkit if not installed already https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html#installing-with-apt
1. Run this command: `sudo docker rm -f aipg-omniworker ; sudo docker run -d -p 7870:8080 --gpus "all" --shm-size 8g --name aipg-omniworker dex3r/aipg-omniworker`
1. Go to http://localhost:7870/ and follow the instructions

# Web configuration

![image](https://github.com/user-attachments/assets/69ea8178-b394-4180-9c02-7d87391027be)

# CLI/Environment configuration parameters

Optionally, you can start the worker when the container is starting, providing parameters in the command:

```
GRID_API_KEY - your API key from https://api.aipowergrid.io/register
WORKER_NAME - image and text worker
WORKER_TYPE - auto/text/image
AUTOSTART_WORKER - true/false - if true, the worker will start as soon as the container boots up
```

e.g.
`docker run -d -p 7870:8080 --gpus "all" --shm-size 8g --mount source=aipg-omniworker-volume,target=/persistent --env GRID_API_KEY=YOUR_GRID_KEY_HERE --env WORKER_NAME=YOUR_WORKER_NAME_HERE WORKER_TYPE=auto --env AUTOSTART_WORKER=true --name aipg-omniworker dex3r/aipg-omniworker`

# Features

- [x] Works on Windows with WSL Docker
- [x] Works on Linux (tested on Debian 22.04 with newest drivers and nvidia toolkit)
- [x] Basic Web Control Panel
- [x] Ability to start from CLI without interacting with Web Control Panel
- [x] Saving config, downloaded models and logs between containers in Docker Volume
- [x] Watchdog to look for worker process crash
- [ ] Watchdog to check worker health
- [x] Periodic auto-restart
- [x] Image worker support
- [x] Text worker support
- [x] Automatic worker selection based on grid image/worker balance
- [ ] Automatic worker selection based on grid image/worker needs (how long is the image/text jobs queue)
- [ ] Multiple GPU support
- [ ] CUDA not installed detection and instructions
- [ ] Text worker/Aphrodide/Image worker/API error detection
- [ ] Windows EXE installer with automatic Docker and CUDA installation
- [ ] Worker stats and graphs

# Development

Use this command to quickly build from source and run the image. Output will be logged to console and the container will be stopped and removed after the command is stopped (e.g. CTRL+C). Don't forget to fill the missing variables `YOUR_GRID_KEY_HERE` and `YOUR_WORKER_NAME_HERE`.

```docker build -t aipg-dotnet-worker . && docker run --rm --mount source=aipg-omniworker-volume,target=/persistent -p 7870:8080 --gpus "all" --shm-size 8g --env GRID_API_KEY=YOUR_GRID_KEY_HERE --env WORKER_NAME=YOUR_WORKER_NAME_HERE --name aipg-omniworker aipg-dotnet-worker```
