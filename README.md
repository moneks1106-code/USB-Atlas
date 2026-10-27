# USB Atlas is now part of InputLab

**USB Atlas has moved into [InputLab](https://github.com/moneks1106-code/InputLab): one free Windows app for your mouse, your controllers and the PC they run on. This repository is no longer updated.**

### ➡️ [Download InputLab](https://github.com/moneks1106-code/InputLab/releases/latest)

### 💬 [Join the InputLab Discord](https://discord.gg/3kEktvD4zm)

## USB Atlas in InputLab

What USB Atlas did, a read-only look at your USB tree with findings that name their evidence, InputLab does in **System › USB map**:

- Every USB controller (probably in the CPU or the chipset, said as an estimate from its PCI ids), every hub, socket and device, with the link speed and how often the bus polls each device.
- One sentence on top says how things stand, and a list says what to check and why, with its source. There is no score.
- Re-plug a device and the map reads itself again and names what moved.
- Click a device to tell the map where its socket sits and what is plugged in next to it. Windows cannot know that.
- The same honesty: the map only reads what Windows reports and changes nothing. It checks the wiring; it does not measure latency.

## New in InputLab

- **Mouse and controller tests:** how evenly the reports of your mouse and your controller reach Windows (host-delivery timing), with a delivery score.
- **USB timing** for mice: a mouse run seen at the USB controller and in the app on one clock.
- **System › Interrupts:** which CPU core holds up your mouse, measured, and a drag-and-drop interrupt layout you can undo.
- **System › Power:** the power settings that are said to slow input, each with Restore.
- **Wireless:** the wireless doctor follows a receiver's way into the PC.
- **Home:** your PC at a glance, every rating with its source.

## Nothing to carry over

InputLab reads your USB tree itself, so there is nothing to import. It does not read USB Atlas's saved snapshots. Those stay in `%LOCALAPPDATA%\UsbAtlas` until you delete that folder; they can contain device serial numbers.

## Old versions

The USB Atlas preview v0.1.5 stays on this page for reference; it was the only release and gets no more updates. Please report problems and results in [InputLab's issues](https://github.com/moneks1106-code/InputLab/issues).
