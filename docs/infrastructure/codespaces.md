# Playing in a GitHub Codespace

A codespace runs the server and serves the web client from GitHub's machines, so the game can be tried with nothing installed but a browser. The repository's dev container (`.devcontainer/devcontainer.json`) prepares the web client and server, then starts them on creation and resume through `node scripts/play-local.mjs --browser --no-open`. Unchanged builds are reused.

The helper is verified locally. Automatic cloud provisioning, detached-process survival after the hooks, and forwarded-port opening remain unverified in a live Codespace; use the manual command below if those hooks do not complete.

## Play

1. Use **Play a branch** on the Pages login screen, or the repository's **Code**, **Codespaces** menu. The link offers an existing Codespace for that branch when available. First setup takes a few minutes.
2. Wait for the forwarded browser port to open. The startup helper prepares the selected branch's server and client together. If the browser did not open, use **Open in Browser** on port 7778 in the Ports panel. The startup output also prints a link with `?play=1` for automatic login and character resume. To start it manually after stopping it, run:

   ```bash
   node scripts/play-local.mjs --browser --no-open
   ```

   `node scripts/play-local.mjs --stop` requests a save and shutdown. Do not start the legacy foreground script while the managed server owns these ports.
3. Keep the forwarded port private for your own playtest. To invite others or connect from the separate Pages origin, make port 7778 public in **Ports**, **Port Visibility**, **Public**, then return it to private when done.
4. The page fills in its own server address. Press **Connect**, or use the `?play=1` link to connect automatically. Create a character on the first visit; quick play resumes the remembered character on later visits.

Other people can join with the same address while the port is public and the codespace runs.

The selected branch must include this startup configuration for automatic preparation. Older branches still use their own dev-container instructions and `scripts/codespace-play.sh`. GitHub's [branch and resume links](https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/setting-up-your-repository/facilitating-quick-creation-and-resumption-of-codespaces) keep each playtest attached to its chosen branch.

## With the client on GitHub Pages

The web client is published to GitHub Pages at <https://arianna-arpg.github.io/hollow-wake-mmo/> from main (`.github/workflows/pages.yml`). Open it and put the codespace's address in the **Server** field: `wss://<codespace-name>-7778.app.github.dev/ws`, or the `https://` address from step 2, which the field turns into the same thing. Port 7778 has to be public for this, since the Pages site is another origin. A link can carry it: `https://arianna-arpg.github.io/hollow-wake-mmo/?server=wss://<codespace-name>-7778.app.github.dev/ws`.

## Notes

- The forwarded address is HTTPS, so the client connects with `wss://`; the codespace forwarder terminates TLS and the server itself speaks plain HTTP and WebSocket.
- Codespaces forwards HTTP only, so the desktop client's TCP port 7777 cannot be reached at a forwarded address. From your own machine, `gh codespace ports forward 7777:7777` tunnels it to `localhost:7777`.
- A codespace stops after a period of inactivity (30 minutes by default) and the server stops with it. Accounts and characters stay in the codespace's `data/` directory until the codespace is deleted.
- For a server that should not be open to everyone, set `auth: token` and a `token` in `config/server.local.yaml` (or `config/server.yaml`) and give players the token; the login panel has a field for it.
- Server settings are in `config/server.yaml`: `websocket_enabled`, `websocket_port`, `web_root` and `chunk_request_radius` govern the browser side.
