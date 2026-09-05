import test from 'node:test';
import assert from 'node:assert/strict';
import {SaveFile} from './save.js';
test('存档字段、导入隔离、原始备份与边界保护',()=>{
  const buffer=new Uint8Array(0x4da3a0);buffer.set(new TextEncoder().encode('UCSD'),4);buffer.fill(255,0x2af40,0x2af40+500*464);buffer.fill(255,0x1b5830,0x1b5830+164*1316);const save=new SaveFile(buffer.buffer);
  save.write(0x20,4,123456);assert.equal(save.read(0x20),123456);assert.equal(save.original[0x20],0);assert.equal(save.characters().length,0);assert.throws(()=>save.write(0x20,1,256));assert.throws(()=>save.read(save.data.length));
  save.addItems([8,9],false,new Map());assert.equal(save.inventory().length,2);assert.equal(save.read(0xa8,3),1);assert.throws(()=>save.addItems(new Array(3800).fill(8),false,new Map()));assert.equal(save.inventory().length,2);
  const character=new Uint8Array(464);character[40]=3;const address=save.importCharacter(character.buffer);assert.equal(save.characters().length,1);assert.equal(save.read(address),1);assert.equal(save.read(address+4),0xffffffff);assert.equal(save.read(0x63984),1);assert.equal(save.read(address+40,1),3);assert.throws(()=>save.importCharacter(new ArrayBuffer(8)));
  assert.throws(()=>new SaveFile(new ArrayBuffer(8)));
});
