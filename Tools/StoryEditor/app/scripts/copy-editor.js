'use strict';
// Встроенная копия редактора для сборки: ../story_editor.html → editor/story_editor.html.
// Приложение само берёт редактор из проекта, а эта копия — запасной вариант (см. main.js).
const fs = require('fs');
const path = require('path');

const source = path.join(__dirname, '..', '..', 'story_editor.html');
const target = path.join(__dirname, '..', 'editor', 'story_editor.html');
fs.mkdirSync(path.dirname(target), { recursive: true });
fs.copyFileSync(source, target);
console.log('Редактор скопирован:', path.relative(process.cwd(), target));
