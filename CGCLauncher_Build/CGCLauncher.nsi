; ============================================================
;  CGCLauncher - NSIS Installer (User-level, no admin required)
;  NSIS 3.x + MUI2 (both bundled with NSIS)
;
;  HOW TO BUILD:
;    1. dotnet publish -c Release -r win-x64 --self-contained false -o ./publish
;    2. Place this .nsi + LICENSE.txt next to the ./publish folder
;    3. Right-click CGCLauncher.nsi → "Compile NSIS Script"
;       or: makensis CGCLauncher.nsi
;
;  OPTIONAL - Remove SmartScreen warning permanently:
;    signtool sign /fd SHA256 /tr http://timestamp.sectigo.com /td SHA256
;                  /f YourCert.pfx /p YourPassword CGCLauncher_Setup.exe
; ============================================================

; ── Includes ─────────────────────────────────────────────────────────────────
; !addincludedir tells NSIS exactly where its own Include folder is.
; This fixes "could not find MUI2.nsh" on setups where the path isn't
; resolved automatically (common when compiling from a custom directory).
!addincludedir "${NSISDIR}\Include"
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"
!include "FileFunc.nsh"     ; provides ${GetSize} for estimated install size

; ── Metadata ──────────────────────────────────────────────────────────────────
!define APP_NAME      "CGCLauncher"
!define APP_LONG_NAME "CrystalGemCraft Launcher"
!define APP_VERSION   "1.0.0"
!define APP_PUBLISHER "CrystalGemCraft"
!define APP_URL       "https://www.crystalgemcraft.com"
!define APP_EXE       "CGCLauncher.exe"
!define APP_ICON      "publish\Assets\logo.ico"
!define DOTNET_URL    "https://dotnet.microsoft.com/en-us/download/dotnet/8.0"

; ── KEY CHANGE: user-level install ───────────────────────────────────────────
; RequestExecutionLevel user means:
;   - NO UAC blue-shield prompt at all
;   - Installs to %LocalAppData% instead of Program Files
;   - Registry written to HKCU instead of HKLM
;   - Works even on accounts without administrator rights
RequestExecutionLevel user

; Uninstall key under HKCU (no admin needed to write here)
!define REG_UNINSTALL \
  "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"

; ── Installer settings ────────────────────────────────────────────────────────
Name              "${APP_LONG_NAME}"
OutFile           "CGCLauncher_Setup.exe"

; Install to the user's own LocalAppData - no admin rights needed
InstallDir        "$LOCALAPPDATA\${APP_NAME}"
InstallDirRegKey  HKCU "${REG_UNINSTALL}" "InstallLocation"

Unicode           True
SetCompressor     /SOLID lzma

; ── MUI2 appearance ───────────────────────────────────────────────────────────
!define MUI_ICON                    "${APP_ICON}"
!define MUI_UNICON                  "${APP_ICON}"
!define MUI_ABORTWARNING

!define MUI_WELCOMEPAGE_TITLE       "Welcome to ${APP_LONG_NAME} Setup"
!define MUI_WELCOMEPAGE_TEXT        \
  "This will install ${APP_LONG_NAME} ${APP_VERSION}.$\r$\n$\r$\n\
   ${APP_LONG_NAME} is a custom Minecraft launcher for the CrystalGemCraft \
   server. Your server will be pre-loaded on first launch.$\r$\n$\r$\n\
   Click Next to continue."

!define MUI_FINISHPAGE_RUN          "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT     "Launch ${APP_NAME} now"
!define MUI_FINISHPAGE_LINK         "Visit CrystalGemCraft"
!define MUI_FINISHPAGE_LINK_LOCATION "${APP_URL}"

; ── Pages ─────────────────────────────────────────────────────────────────────
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE       "LICENSE.txt"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

; ── .NET 8 Desktop Runtime check ─────────────────────────────────────────────
; We can read HKLM even without admin rights - we just can't write to it.
Function CheckDotNet
  StrCpy $0 "0"
  StrCpy $1 "0"

  loop:
    EnumRegValue $2 HKLM \
      "SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App" \
      $1
    StrCmp $2 "" done

    StrCpy $3 $2 2 0    ; grab first 2 chars (StrCopy is not valid NSIS - use StrCpy)
    StrCmp $3 "8." found

    IntOp $1 $1 + 1
    Goto loop

  found:
    StrCpy $0 "1"

  done:
  ${If} $0 == "0"
    MessageBox MB_YESNO|MB_ICONEXCLAMATION \
      ".NET 8 Desktop Runtime was not found.$\r$\n$\r$\n\
       ${APP_NAME} requires the .NET 8 Windows Desktop Runtime.$\r$\n$\r$\n\
       Click Yes to open the Microsoft download page, then re-run this installer.$\r$\n\
       Click No to continue anyway (the launcher won't start without it)." \
      IDNO done_check

    ExecShell "open" "${DOTNET_URL}"
    Abort

    done_check:
  ${EndIf}
FunctionEnd

; ── .onInit ───────────────────────────────────────────────────────────────────
Function .onInit
  ; Require Windows 10 minimum
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_OK|MB_ICONSTOP \
      "${APP_NAME} requires Windows 10 or later."
    Abort
  ${EndIf}

  Call CheckDotNet

  ; Detect existing installation and offer upgrade
  ReadRegStr $0 HKCU "${REG_UNINSTALL}" "DisplayVersion"
  ${If} $0 != ""
  ${AndIf} $0 != "${APP_VERSION}"
    MessageBox MB_YESNO|MB_ICONQUESTION \
      "${APP_LONG_NAME} $0 is already installed.$\r$\n\
       Upgrade to version ${APP_VERSION}?" \
      IDYES continue_install
    Abort
    continue_install:
  ${EndIf}
FunctionEnd

; ── Sections ──────────────────────────────────────────────────────────────────

Section "${APP_LONG_NAME} (required)" SecCore
  SectionIn RO

  SetOutPath "$INSTDIR"
  File /r "publish\*.*"

  ; Write uninstaller
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Add/Remove Programs entry - written to HKCU, no admin needed
  WriteRegStr   HKCU "${REG_UNINSTALL}" "DisplayName"      "${APP_LONG_NAME}"
  WriteRegStr   HKCU "${REG_UNINSTALL}" "DisplayVersion"   "${APP_VERSION}"
  WriteRegStr   HKCU "${REG_UNINSTALL}" "Publisher"        "${APP_PUBLISHER}"
  WriteRegStr   HKCU "${REG_UNINSTALL}" "URLInfoAbout"     "${APP_URL}"
  WriteRegStr   HKCU "${REG_UNINSTALL}" "InstallLocation"  "$INSTDIR"
  WriteRegStr   HKCU "${REG_UNINSTALL}" "UninstallString"  '"$INSTDIR\Uninstall.exe"'
  WriteRegStr   HKCU "${REG_UNINSTALL}" "DisplayIcon"      "$INSTDIR\Assets\logo.ico"
  WriteRegDWORD HKCU "${REG_UNINSTALL}" "NoModify"         1
  WriteRegDWORD HKCU "${REG_UNINSTALL}" "NoRepair"         1

  ; Estimate installed size for the Programs list
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${REG_UNINSTALL}" "EstimatedSize" "$0"

  ; Start Menu shortcut - $SMPROGRAMS points to the user's own Start Menu
  ; when running without admin, so no elevation needed
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut  "$SMPROGRAMS\${APP_NAME}\${APP_LONG_NAME}.lnk" \
                  "$INSTDIR\${APP_EXE}" "" \
                  "$INSTDIR\Assets\logo.ico" 0
  CreateShortcut  "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" \
                  "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Desktop Shortcut" SecDesktop
  CreateShortcut "$DESKTOP\${APP_LONG_NAME}.lnk" \
                 "$INSTDIR\${APP_EXE}" "" \
                 "$INSTDIR\Assets\logo.ico" 0
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore}    "Core launcher files. Required."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "Add a shortcut to your Desktop."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ── Uninstaller ───────────────────────────────────────────────────────────────
Section "Uninstall"
  ; Remove all installed files
  RMDir /r "$INSTDIR"

  ; Remove Start Menu folder
  RMDir /r "$SMPROGRAMS\${APP_NAME}"

  ; Remove Desktop shortcut
  Delete "$DESKTOP\${APP_LONG_NAME}.lnk"

  ; Remove Add/Remove Programs entry from HKCU
  DeleteRegKey HKCU "${REG_UNINSTALL}"

  ; Game data (%AppData%\CGCLauncher) is intentionally left behind
  ; so players keep their downloaded versions and worlds.
SectionEnd
