# ETTerms — Troubleshooting Runbooks

## Serial Port Issues

### COM port is busy / cannot open

**Symptom:** "Access to the port 'COMx' is denied" or "The port is already in use."

**Cause:** Another application (or another ETTerms tab) already has the port open.

**Fix:**
1. Check if another terminal (PuTTY, TeraTerm, Device Manager) has the port open — close it.
2. In ETTerms, only one session per COM port is allowed. Close the existing tab first.
3. If the port is stuck, unplug/replug the USB-Serial adapter.

### COM port not showing in Quick Connect

**Symptom:** The port exists in Device Manager but ETTerms doesn't list it.

**Fix:**
1. Close and reopen the Quick Connect dialog (it scans on open).
2. Check Device Manager → Ports (COM & LPT) for the actual COM number.
3. Some USB adapters need drivers (FTDI, CH340, CP2102).

---

## SSH Issues

### Host key fingerprint changed

**Symptom:** "Host key mismatch" warning when connecting.

**Cause:** The remote server was reinstalled or its SSH keys were regenerated.

**Fix:**
1. Verify with the server admin that the key change is legitimate.
2. Delete the old fingerprint from `%LocalAppData%\ETTerms\known_hosts.json`.
3. Reconnect — ETTerms will prompt to trust the new key.

### SSH connection timeout

**Symptom:** Connection hangs for 30+ seconds then fails.

**Fix:**
1. Verify the host is reachable: `ping <host>` from cmd.
2. Check if SSH port (default 22) is open: `Test-NetConnection <host> -Port 22` in PowerShell.
3. Firewall / VPN may be blocking. Try from a different network.

---

## Script (TTL) Issues

### Script hangs on `wait`

**Symptom:** Script shows "wait 'xxx'" indefinitely.

**Cause:** The expected keyword never appears in the output stream.

**Fix:**
1. Press **■ Stop** to cancel the script.
2. Check that the wait keyword matches exactly (case-sensitive, including spaces).
3. Use `timeout = 10` at the top of your script to auto-fail after 10 seconds instead of waiting forever.
4. Use `flushrecv` before `wait` if there might be stale data in the buffer.

### Script error: "'waitall' can only be used in Group execution mode"

**Symptom:** Script stops immediately with this error.

**Cause:** You used `waitall`, `sendlnall`, or `sendlngroup` commands but ran the script via **▶ Script** (per-tab) or **▶ Run All** instead of **▶ Group**.

**Fix:**
1. Right-click the session tabs → assign them to a Group (1/2/3).
2. Use the **▶ Group1/2/3** buttons in the toolbar to run the script.

---

## PDU Issues

### pduconnect fails

**Symptom:** `[pduconnect] failed to connect to <ip>`

**Fix:**
1. Ping the PDU IP from your machine.
2. Verify the PDU is an iPoMan II/III model (SNMP v1, community "private").
3. Check that SNMP port 161/UDP is not blocked by firewall.

### pductrl returns FAILED

**Symptom:** `[pductrl] device X port Y ON → FAILED`

**Fix:**
1. Ensure you called `pduconnect` first for that device number.
2. Verify the port number is valid (1–12).
3. Check PDU web interface to confirm port is not locked.

---

## General

### Settings not persisting

**Fix:** Settings are saved to `%LocalAppData%\ETTerms\settings.json`. Check that the folder is writable. If the file is corrupted, delete it and restart ETTerms (defaults will be recreated).

### Window position not remembered

**Fix:** Window position saves on close. If ETTerms is killed (Task Manager), position won't be saved. Close normally via the X button.
