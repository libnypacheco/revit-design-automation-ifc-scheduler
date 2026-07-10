# Account Admin Guide: Connecting the Revit to IFC Scheduler to an ACC/BIM 360 Account

This page is for the **ACC/BIM 360 Account Admin**. It is self-contained — you do not need to read the rest of this repository, run any code, or have a developer account.

The Revit to IFC Scheduler reads project files and writes converted IFC files through Autodesk Platform Services (APS). Before it can see your account's projects at all, the scheduler's APS app must be added to your ACC/BIM 360 account as a **Custom Integration**. Only an Account Admin can do this, and it must be done once per ACC/BIM 360 account (tenant).

## What you need from the person requesting this

| Item | Notes |
| --- | --- |
| **Client ID** | A long alphanumeric string identifying the scheduler's APS app. This is *not* secret. |
| **App name** | Any recognizable name, e.g. "Revit to IFC Scheduler". |

You do **not** need the Client Secret. If someone sends it to you, don't paste it anywhere in this flow.

## Steps

1. Go to https://admin.b360.autodesk.com/ and sign in. (EU-hosted accounts redirect to `admin.b360.eu.autodesk.com` — that's fine.)
2. If you administer several accounts, switch to the target account using the selector in the top bar.
3. Open **Settings** and select the **Custom Integrations** tab.
   - The UI follows your Autodesk language preference. In Swedish this is **Inställningar → Anpassade integreringar**.
   - If the Custom Integrations tab is missing, your account may need it activated — see [Autodesk's guide](https://aps.autodesk.com/en/docs/bim360/v1/tutorials/getting-started/get-access-to-account/).
4. Click **Add Custom Integration** (*Lägg till anpassad integrering*).
5. Select access for both **BIM 360 Account Administration** and **Document Management**, then continue.
6. When asked who built the app, choose **I'm the developer** — even if you personally didn't; this option just means the Client ID is supplied directly rather than by invitation.
7. Fill out the final form:
   1. Check the **I have saved the Account ID** confirmation box.
   2. Paste the **Client ID** into the client ID field.
      - Older UI versions label this **"Forge Client ID"** (*Forge Klient-ID*) — Forge is the former name of Autodesk Platform Services; it is the same thing.
   3. Enter the app name.
   4. Save.

## Verifying it worked

Tell the requester the integration is in place. In the scheduler's own **Settings** page, your account should now appear in the "BIM 360 Accounts" list (they may need to refresh). An empty list there means the integration hasn't taken effect or was added to a different account than intended.
