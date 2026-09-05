import {readFile,writeFile,mkdir,copyFile,readdir,cp,rm} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {resolve,dirname} from 'node:path';
const root=dirname(fileURLToPath(import.meta.url));
const destination=resolve(root,'dist');
await rm(destination,{recursive:true,force:true});
await mkdir(resolve(destination,'data'),{recursive:true});
for(const name of ['index.html','app.js','style.css','save.js','mod-engine.js']) await copyFile(resolve(root,name),resolve(destination,name));
const catalog={info:{},templates:{},fortLocations:[]};
for(const [folder,extension,key] of [['info','.txt','info'],['mods','.pchtxt','templates']]) {
  for(const name of await readdir(resolve(root,'../UnicornOverlord',folder))) if(name.endsWith(extension)) catalog[key][name]=await readFile(resolve(root,'../UnicornOverlord',folder,name),'utf8');
}
const source=await readFile(resolve(root,'../UnicornOverlord/Modding/ModCatalog.cs'),'utf8');
for(const match of source.matchAll(/\("([^"]+)", "([^"]+)", (\d+), (\d+)\)/g)) catalog.fortLocations.push({english:match[1],name:match[2],start:Number(match[3]),count:Number(match[4])});
await writeFile(resolve(destination,'data/catalog.json'),JSON.stringify(catalog));
await copyFile(resolve(root,'../UnicornOverlord/info/mission_catalog.json.gz'),resolve(destination,'data/mission_catalog.json.gz'));
await copyFile(resolve(root,'../docs/第三方MOD来源.md'),resolve(destination,'THIRD_PARTY_MODS.txt'));
await cp(resolve(root,'mission/dist'),resolve(destination,'mission'),{recursive:true});
await copyFile(resolve(root,'mission/LICENSE'),resolve(destination,'mission/LICENSE.txt'));
await writeFile(resolve(destination,'.nojekyll'),'');
console.log('静态网站已构建：web/dist（不包含存档、ROM 或本地工具）');
