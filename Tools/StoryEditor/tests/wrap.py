#!/usr/bin/env python3
"""Обернуть story_editor.html в полноценную страницу для тестов: wrap.py <выход.html> [вставка-перед-редактором.html].
Вставка — например mockdb.html (поддельный window.claude.use('db'), как на странице claude.ai)."""
import os
import sys

here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, '..', 'story_editor.html'), encoding='utf-8').read()
mock = open(sys.argv[2], encoding='utf-8').read() if len(sys.argv) > 2 else ''
html = ('<!doctype html><html><head><meta charset=utf8><meta name=viewport content="width=device-width,initial-scale=1">'
        '</head><body>' + mock + src + '</body></html>')
open(sys.argv[1], 'w', encoding='utf-8').write(html)
