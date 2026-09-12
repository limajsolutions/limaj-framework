#!/usr/bin/env bash
# Espelha os slash commands canônicos do template para a raiz do repo do framework.
#
# Por que existe: o Claude Code carrega comandos de `.claude/commands/` na RAIZ do repo
# aberto. A cópia CANÔNICA (a que viaja para cada SaaS) vive em
# `template-backend/.claude/commands/`. Este script copia essa cópia para a raiz para
# que os comandos fiquem ativos ao abrir o limaj-framework no editor.
#
# Direção ÚNICA: template-backend/.claude/commands  →  .claude/commands (raiz).
# Edite sempre a cópia canônica (no template) e rode este script para refletir na raiz.
#
# Uso: bash template-backend/scripts/sync-commands.sh
#      bash template-backend/scripts/sync-commands.sh --check   (só verifica drift; exit 1 se divergir)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SRC="$REPO_ROOT/template-backend/.claude/commands"
DST="$REPO_ROOT/.claude/commands"

if [ ! -d "$SRC" ]; then
  echo "ERRO: cópia canônica não encontrada em $SRC" >&2
  exit 1
fi

if [ "${1:-}" = "--check" ]; then
  if diff -rq "$SRC" "$DST" >/dev/null 2>&1; then
    echo "OK: raiz em sincronia com o template."
    exit 0
  else
    echo "DRIFT: a raiz divergiu do template. Rode sem --check para sincronizar." >&2
    diff -rq "$SRC" "$DST" || true
    exit 1
  fi
fi

mkdir -p "$DST"
# --delete mantém a raiz como espelho exato (remove comandos que saíram do template).
if command -v rsync >/dev/null 2>&1; then
  rsync -a --delete "$SRC/" "$DST/"
else
  rm -f "$DST"/*.md
  cp "$SRC"/*.md "$DST/"
fi

echo "Sincronizado: $(ls -1 "$DST"/*.md | wc -l) comandos em .claude/commands/ (raiz)."
