XrCompositor Protocol Bible
===========================

*TODO: Any kind of security at all. Please don't hurt me.* \
*TODO: Timestamps on input packets, as well as some kind of shared timebase.* \
*TODO: Checksums? We don't need it if we standardize on TLS, I guess.*

Note: All multibyte values are little endian unless otherwise described.

Terminology
===========

- Compositor -- XrCompositor instance running on some device
- Xrc -- This protocol
- Xpra Server -- The actual instance of xpra running on a machine, to which the compositor will connect
- Xrc Client -- Anything connecting to the compositor
- uX -- Unsigned value of X bits
- iX -- Signed value of X bits
- fX -- Floating point value of X bits
- str -- UTF-8 string, prefixed with u32 length; no null terminator
	- If the str is at the end of the packet, it will lack a prefix!
- bool -- 8-bit value containing a 0 if false, non-zero if true (typically 1)
- blob -- Raw byte array prefixed by u32 length

Discovery
=========

The compositor will send UDP broadcasts every \~100ms to port 31337 (TODO: pick an actual port for this) which contains the following information, in order:

- Compositor xrc port as u16
- Device name as str (requires length since we don't have a packet length here)

Xrc Connections
===============

The compositor listens on TCP port 31337 as a default, but the port should be pulled from the discovery packets broadcast by the compositor.

Each discrete operation sent over the wire (in either direction) is a packet of the format:

- Data length as u32 -- This does not include this field or the opcode field!
- Opcode as i32
- Data as u8[length]

Data length is limited to 2GB in the current implementation.

Bidirectional Packets
=====================

Ping (opcode 1)
---------------

**No data**

Upon receiving a Ping packet, the recipient should send a Pong packet.

Pong (opcode 2)
---------------

**No data**

Sent in response to Ping packets.

Xrc Client -> Compositor Packets
================================

Run Script (opcode 1001)
------------------------

- Code as str

This will execute a one-off MoonScript snippet on the compositor. Callback registration will fail silently since the script does not persist.

Load Script (opcode 1002)
-------------------------

- Name as str
- Code as str

Name must be of the form `/^[^\/\\]+\.lua/` -- that is, a valid filename with no path separators and ending in `.lua` (case sensitive).  This will replace any existing script with the same name, causing an unload if such a script is already present. This file will be saved on the device and run at Compositor startup.  It will be executed upon receipt.

Unload Script (opcode 1003)
---------------------------

- Name as str

Name must follow the same restrictions as in Load Script.  If the script exists, it will be unloaded from the Compositor and deleted from disk.

Log Request (opcode 1004)
-------------------------

**No data**

The Compositor will send all log data to the client after receiving this packet

Key Down (opcode 1005) and Key Up (opcode 1006)
-----------------------------------------------

- Keycode as i32

Indicates a key being pressed or released. Keycode is an enum defined in SharpXpra.

Mouse Button Down (opcode 1007) and Mouse Button Up (opcode 1008)
-----------------------------------------------------------------

- Button as u32

Indicates a mouse button being pressed or released. Buttons:

- Left = 0
- Right = 1
- Middle = 2
- Buttons > 2 are simply forwarded across the wire

Mouse Movement (opcode 1009)
----------------------------

- Delta X as i32
- Delta Y as i32

Moves the mouse cursor a certain delta X/Y.  Acceleration is handled on the compositor side.

Display Model (opcode 1010)
---------------------------

- Format as str
- Data as blob

Display a 3d model.  Data must be in the specified format.  Acceptable formats are:

- stl
- glTF

Compositor -> Xrc Client Packets
================================

Log Message (opcode 2001)
-------------------------

- Error as bool
- Message as str

A message from the compositor's log functionality. Error is true if the message is an error, otherwise false.
