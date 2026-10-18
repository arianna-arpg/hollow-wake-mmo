# Appearance evidence

Captures from the two scripted smokes before and after the appearance pipeline ([docs/design/appearance.md](../../docs/design/appearance.md)), on the same server seed and the same scenes, reduced to 1024 pixels wide and JPEG so they stay small. `godot-*` are the desktop client at 1600 by 900, `web-*` the browser client at 1280 by 800 in headless Edge; `before` is main at `b910e94`, `after` the pull request's branch. The hamlet shots are the smoke's first stage on entry; combat is its weaves stage on a hob; cards, grayscale, night and evening are the look stages the smokes gained. The measurement lines the smokes print for these scenes are recorded in [docs/design/sprite-rendering-study.md](../../docs/design/sprite-rendering-study.md).

To reproduce, run a server from a scratch config whose `data_dir` is an empty folder, then:

    godot --path src/HollowWake.Client -- --smoke --shots DIR --host 127.0.0.1 --port PORT
    npm --prefix web run smoke -- --url http://localhost:WSPORT/ --shots DIR
