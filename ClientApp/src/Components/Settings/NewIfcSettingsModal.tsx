import React, {useState} from 'react';
import {observer} from "mobx-react-lite";
import {Checkbox, DefaultButton, Dialog, DialogFooter, DialogType, PrimaryButton, TextField} from "office-ui-fabric-react";
import {Loading} from "../Loading";
import {ApiCalls} from "../../Utilities/ApiCalls";
import {IfcSettingsSet} from "../../Utilities/DataTypes/IfcSettingsSet";
import {appState} from "../../App";

interface INewIfcSettingsModal{
    show: boolean;
    setShow: Function;
}

export const NewIfcSettingsModal = observer(({show, setShow}: INewIfcSettingsModal)=>{
    const [name, setName] = useState("");
    const [loading, setLoading] = useState(false);
    const [exportSettingsJson, setExportSettingsJson] = useState<string | undefined>();
    const [settingsFileName, setSettingsFileName] = useState("");
    const [psetsContent, setPsetsContent] = useState<string | undefined>();
    const [psetsFileName, setPsetsFileName] = useState("");
    const [viewId, setViewId] = useState("");
    const [onlyVisible, setOnlyVisible] = useState(false);

    function toggleHideDialog(){
        setShow(false);
        setName("");
        setExportSettingsJson(undefined);
        setSettingsFileName("");
        setPsetsContent(undefined);
        setPsetsFileName("");
        setViewId("");
        setOnlyVisible(false);
        setTimeout(()=>setLoading(false), 500);
    }

    function readTextFile(file: File, onLoad: (content: string)=>void){
        const reader = new FileReader();
        reader.onload = ()=>onLoad(reader.result as string);
        reader.readAsText(file);
    }

    function onSettingsFileSelected(event: React.ChangeEvent<HTMLInputElement>){
        const file = event.target.files?.[0];
        if (!file) return;
        readTextFile(file, (content)=>{
            setExportSettingsJson(content);
            setSettingsFileName(file.name);
            try {
                const parsed = JSON.parse(content);
                if (parsed.Name && !name) setName(parsed.Name);
            } catch {
                // Not valid JSON; the user can still fix the name manually,
                // and the backend/exporter will surface a clear error.
            }
        });
    }

    function onPsetsFileSelected(event: React.ChangeEvent<HTMLInputElement>){
        const file = event.target.files?.[0];
        if (!file) return;
        readTextFile(file, (content)=>{
            setPsetsContent(content);
            setPsetsFileName(file.name);
        });
    }

    function createSetName(){
        setLoading(true);
        ApiCalls.postIfcSettings({ifcSettings: new IfcSettingsSet({
                name,
                isDefault: !appState.ifcSettings.length,
                exportSettingsJson,
                viewId: viewId || undefined,
                onlyExportVisibleElementsInView: onlyVisible,
                userDefinedPsetsContent: psetsContent
            })})
            .then((newIfcSettings)=>{
                appState.addIfcSettingsSet(new IfcSettingsSet(newIfcSettings));
                toggleHideDialog();
            })
    }

    return (
        <Dialog
            hidden={!show}
            onDismiss={toggleHideDialog}
            minWidth={480}
            dialogContentProps={{
                type: DialogType.normal,
                title: "Create IFC Settings Set",
                subText: "Either enter the name of an IFC export setup saved inside your Revit models (or a Revit built-in setup), or upload a setup JSON exported from Revit's IFC Export dialog (\"Save selected setup\").",
            }}
        >
            {loading ? <Loading/> :
                <form onSubmit={createSetName}>
                    <TextField value={name} onChange={(e, value)=>setName(value || "")} description={"IFC Settings Set Name"}/>

                    <div style={{marginTop: 12}}>
                        <label style={{fontWeight: 600, display: "block", marginBottom: 4}}>Export setup JSON (optional)</label>
                        <input type={"file"} accept={".json,application/json"} onChange={onSettingsFileSelected}/>
                        {settingsFileName && <div style={{fontSize: 12, marginTop: 4}}>Loaded: {settingsFileName}</div>}
                    </div>

                    <div style={{marginTop: 12}}>
                        <label style={{fontWeight: 600, display: "block", marginBottom: 4}}>User-defined property sets .txt (optional)</label>
                        <input type={"file"} accept={".txt,text/plain"} onChange={onPsetsFileSelected}/>
                        {psetsFileName && <div style={{fontSize: 12, marginTop: 4}}>Loaded: {psetsFileName}</div>}
                    </div>

                    <div style={{marginTop: 12}}>
                        <TextField
                            value={viewId}
                            onChange={(e, value)=>setViewId(value || "")}
                            description={"3D View UniqueId (optional; view ids are model-specific; only takes effect with the checkbox below)"}
                        />
                    </div>

                    <div style={{marginTop: 12}}>
                        <Checkbox
                            label={"Only export elements visible in the view (required for the view id to take effect)"}
                            checked={onlyVisible}
                            onChange={(e, checked)=>setOnlyVisible(!!checked)}
                        />
                    </div>

                    <DialogFooter>
                        <PrimaryButton disabled={!name} type={"submit"} text="Create Settings Set" />
                        <DefaultButton onClick={toggleHideDialog} text="Cancel" />
                    </DialogFooter>
                </form>
            }
        </Dialog>
    )
})
