import React, {useEffect, useState} from 'react';
import {observer} from "mobx-react-lite";
import {PrimaryButton} from "office-ui-fabric-react";
import {useTranslation} from "react-i18next";
import {ApiCalls} from "../../Utilities/ApiCalls";

interface IDaStatus {
    nickname?: string;
    engine?: string;
    activityId?: string;
    appBundleProvisioned?: boolean;
    activityProvisioned?: boolean;
    appBundleEngine?: string;
    appBundleVersion?: string;
    activityEngine?: string;
    activityVersion?: string;
}

function parseStatus(data: any): IDaStatus {
    return typeof data === "string" ? JSON.parse(data) : data;
}

export const DesignAutomationPanel = observer(()=>{
    const { t } = useTranslation();
    const [status, setStatus] = useState<IDaStatus | null>(null);
    const [busy, setBusy] = useState(false);

    useEffect(()=>{
        setBusy(true);
        ApiCalls.getDesignAutomationStatus()
            .then((data)=>{ setStatus(parseStatus(data)); setBusy(false); })
            .catch(()=>setBusy(false));
    }, []);

    function provision(){
        setBusy(true);
        ApiCalls.postDesignAutomationProvision()
            .then((data)=>{ setStatus(parseStatus(data)); setBusy(false); })
            .catch(()=>setBusy(false));
    }

    const provisioned = !!(status?.appBundleProvisioned && status?.activityProvisioned);

    return (
        <div style={{padding: "0 16px 16px 16px"}}>
            <p style={{maxWidth: 640}}>
                {t("Conversions run on APS Design Automation for Revit. The RevitIfcExporter appbundle and activity must be provisioned once per APS application (and again after changing the engine).")}
            </p>
            {status &&
                <ul style={{lineHeight: 1.8}}>
                    <li>{t("Nickname")}: <b>{status.nickname}</b></li>
                    <li>{t("Configured engine")}: <b>{status.engine}</b></li>
                    <li>{t("AppBundle")}: <b>{status.appBundleProvisioned ? `${t("Provisioned")} (${status.appBundleEngine}, v${status.appBundleVersion})` : t("Not provisioned")}</b></li>
                    <li>{t("Activity")}: <b>{status.activityProvisioned ? `${t("Provisioned")} (${status.activityEngine}, v${status.activityVersion})` : t("Not provisioned")}</b></li>
                    {status.activityProvisioned && <li>{t("Activity Id")}: <b>{status.activityId}</b></li>}
                </ul>
            }
            <PrimaryButton
                disabled={busy}
                onClick={provision}
                text={busy ? t("Working ...") : (provisioned ? t("Re-provision / Update") : t("Provision Design Automation"))}
            />
        </div>
    )
})
