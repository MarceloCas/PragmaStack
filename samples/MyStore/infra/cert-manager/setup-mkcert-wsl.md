# 🪟 Configurar mkcert no Windows + WSL2

Se você usa **WSL2** no Windows, precisa configurar mkcert em **ambos** os ambientes para ter cadeado verde no navegador Windows.

## 🎯 Por que isso é necessário?

```
Windows (Host)           WSL2 Ubuntu
  │                        │
  ├─ Navegadores          ├─ Docker
  ├─ Trust Store          ├─ K3D
  └─ mkcert CA            └─ cert-manager
       │                       │
       │                       │
       └───────────────────────┘
         MESMA CA! (compartilhada)
```

**Problema:** Se criar CA apenas no WSL, Windows não confiará nela.
**Solução:** Criar CA no Windows e compartilhar com WSL.

---

## 📋 Passo a Passo

### 1️⃣ Instalar mkcert no Windows

**Opção A: Download direto (sem Chocolatey)**

```powershell
# PowerShell como Administrador

# 1. Baixar mkcert
$url = "https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-windows-amd64.exe"
$output = "$env:USERPROFILE\Downloads\mkcert.exe"
Invoke-WebRequest -Uri $url -OutFile $output

# 2. Mover para PATH
Move-Item $output "C:\Windows\System32\mkcert.exe" -Force

# 3. Verificar instalação
mkcert -version

# 4. Instalar CA no Windows
mkcert -install

# 5. Verificar localização da CA
mkcert -CAROOT
# Exemplo: C:\Users\seu-usuario\AppData\Local\mkcert
```

**Opção B: Scoop (alternativa ao Chocolatey)**

```powershell
# PowerShell
scoop install mkcert
mkcert -install
```

**Opção C: winget (Windows Package Manager)**

```powershell
# PowerShell
winget install FiloSottile.mkcert
mkcert -install
```

✅ **Resultado:** Navegadores Windows agora confiam na CA do mkcert!

### 2️⃣ Copiar CA para WSL2

```powershell
# No PowerShell (Windows)

# Criar diretório no WSL
wsl -e mkdir -p ~/.local/share/mkcert

# Copiar arquivos da CA
# ATENÇÃO: Substitua 'seu-usuario' pelo seu usuário Windows!
$usuario = $env:USERNAME
wsl -e cp "/mnt/c/Users/$usuario/AppData/Local/mkcert/rootCA.pem" ~/.local/share/mkcert/
wsl -e cp "/mnt/c/Users/$usuario/AppData/Local/mkcert/rootCA-key.pem" ~/.local/share/mkcert/

# Verificar se copiou
wsl -e ls -la ~/.local/share/mkcert/
```

### 3️⃣ Instalar mkcert no WSL

```bash
# Entrar no WSL
wsl

# Instalar mkcert
wget https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-linux-amd64
chmod +x mkcert-v1.4.4-linux-amd64
sudo mv mkcert-v1.4.4-linux-amd64 /usr/local/bin/mkcert

# Instalar a CA no Linux (usa a CA compartilhada!)
mkcert -install

# Verificar
mkcert -CAROOT
# Deve mostrar: /home/seu-usuario/.local/share/mkcert
```

✅ **Resultado:** Linux (WSL) agora também confia na mesma CA!

### 4️⃣ Configurar cert-manager

```bash
# No WSL
cd infra/cert-manager
./install.sh

# Quando perguntado:
# "Do you want to setup mkcert now? (Y/n):"
# Responda: Y

# O script irá:
# 1. Detectar mkcert ✅
# 2. Pegar a CA de ~/.local/share/mkcert/
# 3. Criar Secret no Kubernetes com a CA
# 4. Criar ClusterIssuer "mkcert"
```

✅ **Resultado:** cert-manager usando a MESMA CA que Windows confia!

---

## 🧪 Testar

### No WSL

```bash
# Criar certificado de teste
kubectl apply -f example-certificate-mkcert.yaml

# Adicionar ao hosts (WSL)
echo "127.0.0.1 example.mystore.local" | sudo tee -a /etc/hosts

# Testar com curl
curl -v https://example.mystore.local
```

### No Windows

```powershell
# Adicionar ao hosts (Windows - PowerShell como Admin)
Add-Content C:\Windows\System32\drivers\etc\hosts "127.0.0.1 example.mystore.local"

# Abrir navegador
start https://example.mystore.local
```

✅ **CADEADO VERDE** no navegador Windows! 🔒

---

## 🔍 Verificação

### Verificar CA no Windows

```powershell
# PowerShell
certutil -user -store "Root" | Select-String "mkcert"
```

Deve mostrar algo como:
```
mkcert DESKTOP-XXXXXX
```

### Verificar CA no WSL

```bash
# WSL
mkcert -CAROOT
ls -la $(mkcert -CAROOT)

# Deve mostrar:
# rootCA.pem
# rootCA-key.pem
```

### Verificar no Kubernetes

```bash
# No WSL
kubectl get secret mkcert-ca-tls -n cert-manager -o yaml

# Deve mostrar o Secret com a CA
```

---

## 🆚 Comparação: Com vs Sem Compartilhamento

### ❌ Sem Compartilhar CA

```
Windows:
  - CA: C:\Users\...\mkcert\rootCA.pem (CA-1)
  - Navegador: Confia em CA-1
  - Acessa https://api.local → ⚠️ Não seguro
    (certificado assinado por CA-2)

WSL:
  - CA: ~/.local/share/mkcert/rootCA.pem (CA-2)
  - cert-manager: Usa CA-2
  - Certificados: Assinados por CA-2
```

### ✅ Com Compartilhamento

```
Windows:
  - CA: C:\Users\...\mkcert\rootCA.pem (CA ÚNICA)
  - Navegador: Confia na CA ÚNICA
  - Acessa https://api.local → 🔒 Seguro!

WSL:
  - CA: ~/.local/share/mkcert/rootCA.pem (MESMA CA)
  - cert-manager: Usa CA ÚNICA
  - Certificados: Assinados pela CA ÚNICA
```

---

## 🐛 Troubleshooting

### "mkcert: not found" no WSL

```bash
# Verificar instalação
which mkcert

# Se não encontrar, reinstalar
wget https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-linux-amd64
chmod +x mkcert-v1.4.4-linux-amd64
sudo mv mkcert-v1.4.4-linux-amd64 /usr/local/bin/mkcert
```

### "Permission denied" ao copiar CA

```powershell
# PowerShell como Administrador
$usuario = $env:USERNAME
icacls "C:\Users\$usuario\AppData\Local\mkcert" /grant "$($usuario):F" /t
```

### Windows ainda mostra "Não seguro"

```powershell
# Reinstalar CA
mkcert -uninstall
mkcert -install

# Reiniciar navegador completamente
# Chrome: Fechar TODAS as janelas e abas
```

### Certificados com domínios errados

```bash
# No WSL, verificar certificado
kubectl get secret example-mystore-tls -o jsonpath='{.data.tls\.crt}' | base64 -d | openssl x509 -noout -text | grep DNS

# Deve mostrar seus domínios
```

---

## 📝 Script Automatizado (Opcional)

Salve como `setup-wsl-mkcert.ps1` no Windows:

```powershell
# setup-wsl-mkcert.ps1
# Executar como Administrador

Write-Host "🔐 Configurando mkcert no Windows + WSL..." -ForegroundColor Cyan

# 1. Instalar mkcert no Windows
if (!(Get-Command mkcert -ErrorAction SilentlyContinue)) {
    Write-Host "📥 Baixando mkcert no Windows..." -ForegroundColor Yellow
    $url = "https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-windows-amd64.exe"
    $output = "$env:TEMP\mkcert.exe"
    Invoke-WebRequest -Uri $url -OutFile $output
    Move-Item $output "C:\Windows\System32\mkcert.exe" -Force
    Write-Host "✅ mkcert instalado!" -ForegroundColor Green
}

# 2. Instalar CA no Windows
Write-Host "🏢 Instalando CA no Windows..." -ForegroundColor Yellow
mkcert -install

# 3. Copiar para WSL
Write-Host "📋 Copiando CA para WSL..." -ForegroundColor Yellow
$usuario = $env:USERNAME
wsl -e mkdir -p ~/.local/share/mkcert
wsl -e cp "/mnt/c/Users/$usuario/AppData/Local/mkcert/rootCA.pem" ~/.local/share/mkcert/
wsl -e cp "/mnt/c/Users/$usuario/AppData/Local/mkcert/rootCA-key.pem" ~/.local/share/mkcert/

# 4. Instalar mkcert no WSL
Write-Host "📥 Instalando mkcert no WSL..." -ForegroundColor Yellow
wsl -e bash -c "wget -q https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-linux-amd64 -O /tmp/mkcert && chmod +x /tmp/mkcert && sudo mv /tmp/mkcert /usr/local/bin/mkcert"

# 5. Instalar CA no WSL
Write-Host "🏢 Instalando CA no WSL..." -ForegroundColor Yellow
wsl -e mkcert -install

Write-Host "✅ Configuração concluída!" -ForegroundColor Green
Write-Host ""
Write-Host "Próximos passos:"
Write-Host "1. No WSL: cd infra/cert-manager && ./install.sh"
Write-Host "2. Escolha 'Y' quando perguntado sobre mkcert"
Write-Host ""
```

Executar:
```powershell
# PowerShell como Administrador
.\setup-wsl-mkcert.ps1
```

---

## 🎯 Resumo

| Etapa | Windows | WSL | Resultado |
|-------|---------|-----|-----------|
| 1. Instalar mkcert | ✅ Download direto | ✅ wget + install | Ambos têm mkcert |
| 2. Criar/Copiar CA | ✅ mkcert -install | ✅ Copiar de Windows | MESMA CA |
| 3. Confiar na CA | ✅ Automático | ✅ mkcert -install | Ambos confiam |
| 4. cert-manager | - | ✅ ./install.sh | Usa CA compartilhada |
| 5. Navegador | 🔒 Cadeado verde | 🔒 Cadeado verde | HTTPS válido! |

---

## 💡 Alternativa: Acesso Remoto

Se você **não quiser** compartilhar CAs, pode acessar do WSL:

```bash
# No WSL
export DISPLAY=:0

# Instalar navegador
sudo apt install firefox

# Abrir
firefox https://example.mystore.local
```

Mas é menos prático que usar navegador Windows nativo.

---

## 📚 Referências

- [mkcert GitHub](https://github.com/FiloSottile/mkcert)
- [WSL2 Networking](https://docs.microsoft.com/en-us/windows/wsl/networking)
- [Windows Certificate Store](https://docs.microsoft.com/en-us/windows-server/administration/windows-commands/certutil)
