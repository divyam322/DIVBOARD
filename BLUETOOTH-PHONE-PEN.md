# DIVBOARD Bluetooth Phone Pen (experimental)

This adds a black phone drawing pad that sends white-ink points over Bluetooth LE to a Windows receiver, which forwards them to the DIVBOARD page through a local WebSocket.

## Important compatibility note
This uses the Windows BLE **peripheral/GATT server** role. Many PC adapters/drivers do not expose this role, even when ordinary Bluetooth works. The receiver reports a clear error if Windows refuses to create/advertise the GATT service. The adapter model name alone cannot guarantee support.

## Files
- `phone-pad.html`: open on the phone in Chrome from GitHub Pages.
- `WindowsReceiver/`: small .NET 8 Windows app source.
- `index.html`: connects to the local bridge and inserts received strokes into DIVBOARD's existing object history.

## Run the Windows receiver
1. Install the .NET 8 SDK for Windows from Microsoft: https://dotnet.microsoft.com/download/dotnet/8.0
2. Open PowerShell in the `WindowsReceiver` folder.
3. Run `dotnet run`.
4. Leave the receiver running. If Windows reports that it cannot create or advertise the GATT service, the adapter/driver does not support the required BLE peripheral role; don't change phone settings to work around it.

## Connect the phone
1. On the phone, open `https://divyam322.github.io/DIVBOARD/phone-pad.html` in Chrome.
2. Tap **Connect to laptop** and select the DIVBOARD receiver.
3. On the laptop, open DIVBOARD and look for the Bluetooth status indicator near the top-right. It connects to `ws://127.0.0.1:8765/`.
4. Write on the phone's black canvas; white strokes should appear on DIVBOARD.

## Security
The receiver listens only on loopback (127.0.0.1), not on the network. Bluetooth writes are not encrypted at the application layer; use this only with your own devices in a trusted environment. Stop the receiver when not using it.

## Troubleshooting
- If the phone cannot find the receiver, Windows BLE peripheral advertising is probably unavailable on this adapter/driver.
- If DIVBOARD shows the bridge as offline, make sure the receiver is running before opening/reloading DIVBOARD.
- This is an experimental first version; test before relying on it for classwork.
