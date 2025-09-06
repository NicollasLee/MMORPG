# MMORPG (Unity)

Este repositório contém somente os **arquivos necessários** para abrir o projeto no Unity.

## ✅ O que está versionado
- `Assets/` (scripts, cenas, prefabs, materiais e `.meta`)
- `Packages/`
- `ProjectSettings/`

## 🚫 O que fica fora do repositório
- Caches e arquivos gerados pelo Unity/IDE: `Library/`, `Temp/`, `Logs/`, `Obj/`, `.vs/`
- **Assets muito grandes** (ex.: arquivos HDRI `.hdr`, mapas de iluminação `.exr`, vídeos), que podem ser baixados à parte.

## 📦 Dependências de assets (baixe à parte)
Liste aqui os pacotes/arquivos grandes e onde baixá-los. Exemplos:

- **Stylized Nature Kit Lite** — Proxy Games — (Asset Store)  
  Coloque o link aqui e, depois de baixar, posicione os arquivos dentro de:
  `Assets/Proxy Games/Stylized Nature Kit Lite/...`

- **HDRI de iluminação** (opcional)  
  Coloque o link aqui. Após baixar, coloque o arquivo em:
  `Assets/Proxy Games/Stylized Nature Kit Lite/Misc/HDRI.hdr`

> Observação: se você não tiver esses assets, o projeto abre, mas algumas cenas podem ficar sem iluminação/skybox ou com materiais padrão.

## 🛠️ Como abrir o projeto
1. Instale a mesma versão do Unity usada neste projeto (ver `ProjectSettings/ProjectVersion.txt`).  
2. Clone o repositório:
   ```bash
   git clone <seu-repo>
   ```
3. (Opcional) Baixe os assets grandes listados acima e coloque nas pastas indicadas.
4. Abra o projeto pelo Unity Hub.

## 🧹 Convenções de versão (Git)
Este repo usa `.gitignore` para manter apenas o essencial. Se um arquivo grande for adicionado por engano:
```bash
git rm --cached caminho/do/arquivo
git commit -m "chore: remove arquivo grande e ignora"
```
Se já tiver ido para o histórico, limpe com:
```bash
# requer git-filter-repo instalado
git filter-repo --path caminho/do/arquivo --invert-paths
git push --force-with-lease
```

## 📄 Licenças
Respeite as licenças dos assets baixados externamente.
