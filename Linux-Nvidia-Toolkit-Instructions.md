How to install required NVIDIA Drivers and NVIDIA Containter Toolkit short instructions.
Full version of this is available here: https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html

This instruction will work if you are using APT. Otherwise, follow the instructions above.

# Check if you have compatible Docker version

Run `sudo systemctl restart docker`. If this command fails, reinstall Docker following instruction for your distro here: https://docs.docker.com/engine/install/

<details>

<summary>Drivers</summary>

## Ubuntu
1. Run `sudo ubuntu-drivers install --gpgpu`

## Debian

1. Run `sudo apt update`
1. Run `sudo apt install nvidia-driver firmware-misc-nonfree`

## Mint

1. Run `sudo add-apt-repository ppa:graphics-drivers/ppa`
1. Run `sudo apt update`
1. Run `sudo apt install nvidia-driver-555`

## Other Distros

Use `.run` from here: https://www.nvidia.com/Download/index.aspx?lang=en-us

Or follow the instructions: https://docs.nvidia.com/datacenter/tesla/tesla-installation-notes/index.html

</details>

<details>
<summary>Nvidia Container Toolkit</summary>

Run those commands:
```
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg \
  && curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | \
    sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
    sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list
```

```sudo apt-get update```

```sudo apt-get install -y nvidia-container-toolkit```

```sudo nvidia-ctk runtime configure --runtime=docker```

```sudo systemctl restart docker```
  
</details>
