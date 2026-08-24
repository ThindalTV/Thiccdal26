# Showing the teleprompter

The teleprompter is a web page that Thiccdal serves at `/prompter`. You display it as a custom
browser dock inside OBS, so there is nothing extra to download, install, or keep up to date.

The dock is a normal OBS panel. Float it, drag it to the monitor you read from, and resize it to
suit. OBS remembers the position between sessions.

> The teleprompter has no controls of its own. You scroll it and trigger everything else from the
> dashboard on your control device. See [Pre-live workflow](./pre-live-workflow.md).

## Add the teleprompter dock to OBS

1. Start Thiccdal on your stream PC and note the address it is listening on, usually
   `http://localhost:5000`.
2. In OBS, select **Docks** > **Custom Browser Docks**.
3. Enter a **Dock Name** of `Teleprompter`.
4. Enter the **URL** `http://localhost:5000/prompter`.
5. Select **Apply**, then **Close**.

The dock opens with the teleprompter in it. If the dock is blank, confirm Thiccdal is running and
open the same URL in a normal browser to check.

## Position the dock on your reading monitor

1. Drag the **Teleprompter** dock by its title bar until it detaches from the OBS window.
2. Move the floating window to the monitor you read from and resize it.
3. To hide the title bar, right-click the dock title and clear **Show title bar**.

OBS saves floating dock positions per profile, so this is a one-time setup.

## Use HTTP, not HTTPS

Use the `http://` address for the dock. Thiccdal serves every page over both HTTP and HTTPS, and
it applies no HTTPS redirect, so both work in a normal browser. The browser engine inside OBS has
no way to accept the development certificate, so an `https://` dock shows a certificate error you
cannot dismiss.

## Connect Thiccdal to OBS

Thiccdal can read the OBS stream state over obs-websocket. When you enable this, the pre-live
checklist gains an automatic **OBS connected** item and Thiccdal knows when you start and stop
streaming.

1. In OBS, select **Tools** > **WebSocket Server Settings**.
2. Select **Enable WebSocket server**.
3. Note the **Server Port**, which defaults to `4455`. If **Enable Authentication** is selected,
   select **Show Connect Info** to read the password.
4. In your Thiccdal `appsettings.json`, set `Obs:Enabled` to `true`. Set `Obs:Port` and
   `Obs:Password` if they differ from the defaults.
5. Restart Thiccdal.

Thiccdal connects on startup and reconnects on its own if you close and reopen OBS. Leaving
`Obs:Enabled` set to `false` is fine — the teleprompter dock works without it, and the checklist
does not show the **OBS connected** item.

## Show the teleprompter on a second device

The teleprompter is a web page, so any device on the same network can display it. Browse to
`http://<stream-pc-name>:5000/prompter` from a tablet or a spare laptop. This works alongside the
OBS dock, and both stay in sync.

## Related

- [Pre-live workflow](./pre-live-workflow.md)
- [Getting started](./getting-started.md)
- [Connecting to Twitch](./connecting-to-twitch.md)
