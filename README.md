This is a repository for AIPG Omniworker. It's a tool for easy setup and management of single or multiple Image and/or Text workers in [AIPG Network](https://aipowergrid.io/). 

# Installing

## Windows

1. Install [Docker](https://www.docker.com/products/docker-desktop/) if not installed already.
1. Install [CUDA Toolkit](https://developer.nvidia.com/cuda-downloads?target_os=Windows&target_arch=x86_64) if not installed already.
1. Run this command: `(docker rm -f aipg-omniworker || ver > nul) && docker pull dex3r/aipg-omniworker && docker run -d -p 7870:8080 --gpus "all" --shm-size 8g --mount source=aipg-omniworker-volume,target=/persistent --name aipg-omniworker dex3r/aipg-omniworker`
1. Go to http://localhost:7870/ and follow the instructions

## Linux

1. Install [Docker CE](https://docs.docker.com/engine/install/) (not Docker Desktop nor Snap Docker) if not installed already.
1. Install [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html#installing-with-apt) if not installed already.
1. Run this command: `sudo -- sh -c "docker rm -f aipg-omniworker ; sudo docker pull dex3r/aipg-omniworker ; sudo docker run -d -p 7870:8080 --gpus 'all' --shm-size 8g --mount source=aipg-omniworker-volume,target=/persistent --name aipg-omniworker dex3r/aipg-omniworker"`
1. Go to http://localhost:7870/ and follow the instructions

## Running from source

1. Install Docker and Nvidia software from the steps above.
1. Clone this repository.
1. Clone submodules: `git submodule init` and `git submodule update`
1. Run the command from #Development section below.

# Web configuration

This is the recommended configuration method. Just go to http://localhost:7870/ and follow the instructions.

![image](https://github.com/user-attachments/assets/423df1b9-44de-4877-a2dd-2b6ad2b9246c)

# Alternative setup (advanced)

Optionally, you can configure workers by modifying the config files directly and providing environmental variables. This is much harder than using the web frontend.

Variables:

```
GRID_API_KEY - your API key from https://api.aipowergrid.io/register
WORKER_NAME - you unique worker name
```

e.g.
`docker run -d -p 7870:8080 --gpus "all" --shm-size 8g --mount source=aipg-omniworker-volume,target=/persistent --env GRID_API_KEY=YOUR_GRID_KEY_HERE --env WORKER_NAME=YOUR_WORKER_NAME_HERE --name aipg-omniworker dex3r/aipg-omniworker`

Configuration files can be found in a docker volume.

1. Run the image for a few seconds.
1. Find Docker volume named `aipg-omniworker-volume`.
1. Modify the following files in the `config/` directory: `userConfig.yaml`, `instanceConfig_0.yaml`. Optionally also modify: `bridgeData.yaml`, `imageBridgeData.yaml`, `textWorkerConfig.yaml`.
1. Duplicate `instanceConfig_0.yaml` to match the desired number of workers, renaming files with increasing N (N >= 0) as this template: `instanceConfig_N.yaml`.
1. Inside each `instanceConfig_N.yaml` set `AutoStartWorker` to `true`.
1. Run the container.

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
- [x] Multiple GPU support
- [ ] CUDA not installed detection and instructions
- [ ] Text worker/Aphrodide/Image worker/API error detection
- [ ] Windows EXE installer with automatic Docker and CUDA installation
- [ ] Worker stats and graphs

# Development

Use this command to quickly build from source and run the image. Output will be logged to console and the container will be stopped and removed after the command is stopped (e.g. CTRL+C). Don't forget to fill the missing variables `YOUR_GRID_KEY_HERE` and `YOUR_WORKER_NAME_HERE`.

```docker build -t aipg-dotnet-worker . && docker run --rm --mount source=aipg-omniworker-volume,target=/persistent -p 7870:8080 --gpus "all" --shm-size 8g --env GRID_API_KEY=YOUR_GRID_KEY_HERE --env WORKER_NAME=YOUR_WORKER_NAME_HERE --name aipg-omniworker aipg-dotnet-worker```
