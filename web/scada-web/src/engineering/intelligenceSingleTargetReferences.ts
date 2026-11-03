import type { SingleTargetSignal } from './IntelligenceSingleTargetDeviationPanel';
const UUID=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function readSingleTargetSignals(json:string):readonly SingleTargetSignal[]|null {
 if(typeof json!=='string'||json.length>16384)return null;
 try{const items=JSON.parse(json);if(!Array.isArray(items)||items.length<2||items.length>16)return null;
 const ids=new Set<string>(),tags=new Set<string>();let condition=false;
 for(const item of items){
 if(!item||typeof item!=='object'||Array.isArray(item)||Object.keys(item).length!==6||Object.keys(item).some(key=>!['id','tagId','label','unit','canonicalUnit','unitCode'].includes(key)))return null;
 for(const [value,set] of [[item.id,ids],[item.tagId,tags]] as const){if(typeof value!=='string'||!UUID.test(value)||value==='00000000-0000-0000-0000-000000000000'||set.has(value.toLowerCase()))return null;set.add(value.toLowerCase());}
 if(typeof item.label!=='string'||!item.label.trim()||item.label.length>320||item.label!==item.label.trim())return null;
 if(!(item.unit===null||typeof item.unit==='string'&&item.unit.length<=128))return null;
 if(item.canonicalUnit===null&&item.unitCode===null)continue;
 if(typeof item.canonicalUnit!=='string'||!item.canonicalUnit.trim()||item.canonicalUnit.length>32||typeof item.unitCode!=='string'||!item.unitCode.trim()||item.unitCode.length>160)return null;
 condition=true;
 }
 return condition?items:null;
 }catch{return null;}
}
