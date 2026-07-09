import {computed, observable} from "mobx";

export interface IIfcSettingsSet {
    id?: string;
    name: string;
    isDefault: boolean;
    exportSettingsJson?: string | null;
    viewId?: string | null;
    onlyExportVisibleElementsInView?: boolean;
    userDefinedPsetsContent?: string | null;
}

export class IfcSettingsSet{
    public readonly id?: string;
    @observable public name: string;
    @observable public isDefault: boolean;
    @observable public exportSettingsJson?: string | null;
    @observable public viewId?: string | null;
    @observable public onlyExportVisibleElementsInView: boolean;
    @observable public userDefinedPsetsContent?: string | null;

    constructor({id, name, isDefault, exportSettingsJson, viewId, onlyExportVisibleElementsInView, userDefinedPsetsContent}: IIfcSettingsSet) {
        this.id = id;
        this.name = name;
        this.isDefault = isDefault;
        this.exportSettingsJson = exportSettingsJson;
        this.viewId = viewId;
        this.onlyExportVisibleElementsInView = onlyExportVisibleElementsInView || false;
        this.userDefinedPsetsContent = userDefinedPsetsContent;
    }

    @computed public get listValue(){
        return {
            label: this.name,
            value: this.name
        }
    }

    @computed public get hasSettingsFile(){
        return !!this.exportSettingsJson;
    }

    @computed public get searchTerm(){return `${this.name}`.toLowerCase()}
}
