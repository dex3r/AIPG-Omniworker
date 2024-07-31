This repository allows you to set up a AIPG Worker.

# Installing

## Windows

1. Install Docker if not installer already https://www.docker.com/products/docker-desktop/
1. Run this command: `docker run -d -p 7870:8080 --gpus "all" --shm-size 8g --name aipg-omniworker dex3r/aipg-omniworker:0.1`
1. Go to http://localhost:7870/ and follow the instructions
