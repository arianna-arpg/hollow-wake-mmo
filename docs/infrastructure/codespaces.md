# Playing in a GitHub Codespace

A codespace runs the server and serves the web client from GitHub's machines, so the game can be tried with nothing installed but a browser. The repository's dev container (`.devcontainer/devcontainer.json`) is a .NET 10 image with Node 22; creating the codespace builds the solution and the web client once.

## Play

1. On the repository page choose **Code**, **Codespaces**, **Create codespace on main** (or on any branch). The first start takes a few minutes while the solution and the web client build.
2. In the codespace terminal run:

   ```bash
   scripts/codespace-play.sh
   ```

   It builds the web client if needed and starts the server: TCP on port 7777 for the desktop client, and on port 7778 the WebSocket game socket plus the web client itself. It prints the web client's address, `https://<codespace-name>-7778.app.github.dev/`.
3. Make port 7778 public: in the **Ports** panel, right-click port 7778 (labelled *Web client and WebSocket*), choose **Port Visibility**, **Public**. `scripts/codespace-play.sh --public` asks the GitHub CLI to do this for you. Anyone with the address can then reach the server, so switch it back to private, or stop the codespace, when you are done.
4. Open the address in a browser. The page is served by the game server, so it fills in its own address; press **Connect**.
5. Log in with any account name (development auth creates accounts on first use), create a character and press **Enter the Weft**.

Other people can join with the same address while the port is public and the codespace runs.

## With the client on GitHub Pages

When the repository publishes the web client to GitHub Pages (`.github/workflows/pages.yml`), open the Pages address and put the codespace's address in the **Server** field: `wss://<codespace-name>-7778.app.github.dev/ws`, or the `https://` address from step 2, which the field turns into the same thing. Port 7778 has to be public for this, since the Pages site is another origin. A link can carry it: `https://<owner>.github.io/<repository>/?server=wss://<codespace-name>-7778.app.github.dev/ws`.

## Notes

- The forwarded address is HTTPS, so the client connects with `wss://`; the codespace forwarder terminates TLS and the server itself speaks plain HTTP and WebSocket.
- Codespaces forwards HTTP only, so the desktop client's TCP port 7777 cannot be reached at a forwarded address. From your own machine, `gh codespace ports forward 7777:7777` tunnels it to `localhost:7777`.
- A codespace stops after a period of inactivity (30 minutes by default) and the server stops with it. Accounts and characters stay in the codespace's `data/` directory until the codespace is deleted.
- For a server that should not be open to everyone, set `auth: token` and a `token` in `config/server.local.yaml` (or `config/server.yaml`) and give players the token; the login panel has a field for it.
- Server settings are in `config/server.yaml`: `websocket_enabled`, `websocket_port`, `web_root` and `chunk_request_radius` govern the browser side.
