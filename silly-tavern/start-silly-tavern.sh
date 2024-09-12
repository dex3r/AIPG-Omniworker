#!/usr/bin/env bash

if ! command -v npm &> /dev/null
then
	echo "Installing nvm..."
	export NVM_DIR="$([ -z "${XDG_CONFIG_HOME-}" ] && printf %s "${HOME}/.nvm" || printf %s "${XDG_CONFIG_HOME}/nvm")"
	[ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
	curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.3/install.sh | bash
	export NVM_DIR="$HOME/.nvm"
	nvm install --lts
	nvm use --lts
fi

echo "Installing Node Modules..."
export NODE_ENV=production
npm i --no-audit --no-fund --quiet --omit=dev

echo "Entering SillyTavern..."
node "server.js" "$@"