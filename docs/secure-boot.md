# Turning Secure Boot off (and back on)

Secure Boot is a setting in your PC's firmware (the software that runs before Windows starts).
It only lets approved software start the PC, which protects against some kinds of hidden
malware. Some Linux distros, including Pop!_OS, can't start while it's on.

Turning it off doesn't delete anything and doesn't change Windows. You can turn it back on
later the same way. However, a distro that needs it off (like Pop!_OS) won't start while
it's on, so only turn it back on if you go back to using Windows alone.

GreyHaven never changes this setting for you. These are instructions to follow yourself.

## Before you start

1. **Save your BitLocker recovery key.** If BitLocker or "Device encryption" is on (GreyHaven's
   report tells you), Windows may ask for the recovery key the next time it starts after a
   firmware change. Without the key you can't get into Windows. Find it at
   [aka.ms/myrecoverykey](https://aka.ms/myrecoverykey) (sign in with your Microsoft account)
   and write it down or print it.
2. **Check whether your PC maker has its own guide.** GreyHaven links it when it recognises
   your PC. Their steps match your firmware screens exactly.
3. **Take a photo** of each firmware screen before changing anything, so you can put it back.

## Open the firmware settings from Windows

This works on most Windows 10 and 11 PCs without needing to know a special key:

1. Open **Settings** → **System** → **Recovery** (on Windows 10: **Update & Security** →
   **Recovery**).
2. Next to **Advanced startup**, click **Restart now**.
3. Choose **Troubleshoot** → **Advanced options** → **UEFI Firmware Settings** → **Restart**.

If you don't see "UEFI Firmware Settings", restart and press the setup key as soon as the
maker's logo appears. It's often **F2**, **Del**, **F10** or **F1**, and the logo screen
usually says which.

## Turn Secure Boot off

Firmware screens differ between makers, but the steps are similar:

1. Look for a tab called **Security**, **Boot** or **Authentication**.
2. Find **Secure Boot** (sometimes inside **Secure Boot Configuration** or
   **Secure Boot Control**).
3. Set it to **Disabled** (or **Off**).
   - If the option is greyed out, some PCs require you to set a **supervisor password**
     first. If you set one, write it down.
   - Don't change other settings, especially **Boot Mode**. Switching from UEFI to
     "Legacy"/"CSM" can stop Windows from starting.
4. **Save and exit**, usually with **F10**, then confirm.

To check it worked, open the Start menu, type `msinfo32`, and look at **Secure Boot State**.
It should say **Off**.

## Turning it back on

Follow the same steps and set Secure Boot back to **Enabled**. If you set a supervisor
password only for this change, you can remove it afterwards.

## More help

- Microsoft: [Disabling Secure Boot](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/disabling-secure-boot)
- Your PC or motherboard maker's support site: search for your model plus "Secure Boot".
