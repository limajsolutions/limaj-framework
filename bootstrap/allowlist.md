# Bootstrap Allowlist (Source of Truth)

- Source (read-only): `/media/tvlima/STORAGE/Repos/<legacy-source-repo>`
- Target: `./` (repositório atual `limaj-framework`)
- Regra: copiar somente itens explicitamente allowlisted; nunca copiar o repositório inteiro.

## Copiar agora (arquivos genéricos)

- `.claude/commands/analyst.md`
- `.claude/commands/spike.md`
- `.claude/commands/issues.md`
- `.claude/commands/impl.md`
- `.claude/commands/bugfix.md`
- `.claude/commands/review.md`
- `.claude/commands/infra.md`
- `.claude/commands/flow.md`

## Adaptar antes de copiar

- Qualquer arquivo que contenha:
- nomes de produto, exemplos, paths ou contexto específicos
- namespaces/projetos específicos (ex.: nomes de solução, assemblies, csproj)
- referências de produto/domínio (entidades, regras de negócio, backlog legado)
- configuração de ambiente acoplada (resource names, IDs, URLs reais)

## Não copiar

- artefatos de seed de dados de produto
- qualquer arquivo/pasta com nome ou conteúdo específico de um produto
- código de domínio, features de negócio e migração de legado
- segredos, variáveis sensíveis e arquivos de ambiente reais
