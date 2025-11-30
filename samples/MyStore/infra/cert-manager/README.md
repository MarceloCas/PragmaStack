# 🔐 MyStore cert-manager

Automação de certificados TLS/SSL com **mkcert** para desenvolvimento local e cert-manager para produção.

## 🎯 O que você vai aprender

Neste guia você vai entender:
- ✅ **O que é uma CA** (Certificate Authority) e como funciona a cadeia de confiança
- ✅ **Por que mkcert é mágico** (cadeado verde 🔒 localmente)
- ✅ **Como cert-manager automatiza** criação e renovação de certificados
- ✅ **Diferença entre mkcert, Let's Encrypt e Self-Signed**
- ✅ **Como ter HTTPS de verdade** no seu ambiente local de desenvolvimento

## ⚡ Início Rápido (para os apressados)

```bash
# 1. (OPCIONAL) Instalar mkcert para cadeado verde no navegador

# Windows - Download direto (PowerShell como Admin):
Invoke-WebRequest -Uri https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-windows-amd64.exe -OutFile mkcert.exe
Move-Item mkcert.exe C:\Windows\System32\mkcert.exe -Force

# OU Windows com winget:
# winget install FiloSottile.mkcert

# OU Windows com scoop:
# scoop install mkcert

# macOS:
# brew install mkcert

# 2. No WSL (onde roda K3D), rodar instalador
cd infra/cert-manager
./install.sh

# 3. Verificar qual ClusterIssuer foi criado
kubectl get clusterissuers

# Se você configurou mkcert no install.sh → ClusterIssuer "mkcert" existe
# Se NÃO configurou mkcert → Precisa criar self-signed manualmente:
#   kubectl apply -f clusterissuer-selfsigned.yaml

# 4. Criar certificado wildcard (RECOMENDADO - 1 cert para tudo!)

# Se você tem o ClusterIssuer "mkcert":
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: mystore-wildcard-cert
  namespace: default
spec:
  secretName: mystore-wildcard-tls
  issuerRef:
    name: mkcert
    kind: ClusterIssuer
  dnsNames:
    - "*.mystore.local"
    - mystore.local
EOF

# OU se você criou o ClusterIssuer "selfsigned":
# cat <<EOF | kubectl apply -f -
# apiVersion: cert-manager.io/v1
# kind: Certificate
# metadata:
#   name: mystore-wildcard-cert
#   namespace: default
# spec:
#   secretName: mystore-wildcard-tls
#   issuerRef:
#     name: selfsigned
#     kind: ClusterIssuer
#   dnsNames:
#     - "*.mystore.local"
#     - mystore.local
# EOF

# 5. Aguardar e verificar
kubectl wait --for=condition=ready certificate mystore-wildcard-cert --timeout=60s
kubectl get certificate mystore-wildcard-cert
kubectl get secret mystore-wildcard-tls

# 6. Pronto! Você tem:
# ✅ cert-manager instalado
# ✅ ClusterIssuer configurado
# ✅ Certificado wildcard válido para TODOS os subdomínios *.mystore.local
# ✅ Secret "mystore-wildcard-tls" criado e pronto para usar

# ⚠️ IMPORTANTE: O certificado está PRONTO mas ainda não serve HTTPS!
# Para servir HTTPS, você precisa:
# - Instalar um Ingress Controller (Kong, nginx, etc.) - próxima fase do tutorial
# - Criar um Ingress que usa o Secret "mystore-wildcard-tls"
# Por enquanto, apenas verifique que o certificado foi criado com sucesso.
```

**Resultado:** Um certificado wildcard que funciona para api.mystore.local, app.mystore.local, admin.mystore.local e qualquer outro subdomínio! 🎉

> **💡 O que você acabou de criar?**
>
> Você criou um **Secret** do tipo TLS que contém:
> - `tls.crt` - Certificado público (chave pública)
> - `tls.key` - Chave privada
> - `ca.crt` - Certificado da CA (opcional)
>
> **O que esse certificado FAZ:**
> - ✅ Existe como Secret no Kubernetes
> - ✅ Pode ser referenciado em Ingress/Gateway
> - ✅ Será renovado automaticamente pelo cert-manager
>
> **O que esse certificado NÃO FAZ:**
> - ❌ Não serve HTTPS automaticamente
> - ❌ Não abre porta 443
> - ❌ Não cria servidor web
>
> **Para usar o certificado:**
> Você precisa de um **Ingress Controller** (Kong, nginx-ingress, Traefik, etc.)
> que vai **ler o Secret** e **servir HTTPS** usando esse certificado.
>
> Isso será configurado nas próximas fases do tutorial! 🚀

> **🪟 Usando WSL2 no Windows?**
>
> Se você roda K3D dentro do WSL2 mas usa navegador no Windows, precisa compartilhar a CA entre os dois ambientes.
>
> 📖 **Guia completo:** [setup-mkcert-wsl.md](setup-mkcert-wsl.md)
>
> **TL;DR:**
> ```powershell
> # 1. No Windows (PowerShell Admin) - baixar e instalar mkcert
> Invoke-WebRequest -Uri https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-windows-amd64.exe -OutFile mkcert.exe
> Move-Item mkcert.exe C:\Windows\System32\mkcert.exe -Force
> mkcert -install
>
> # 2. Copiar CA para WSL
> $usuario = $env:USERNAME
> wsl -e mkdir -p ~/.local/share/mkcert
> wsl -e cp "/mnt/c/Users/$usuario/AppData/Local/mkcert/rootCA.pem" ~/.local/share/mkcert/
> wsl -e cp "/mnt/c/Users/$usuario/AppData/Local/mkcert/rootCA-key.pem" ~/.local/share/mkcert/
>
> # 3. No WSL, instalar mkcert e continuar com ./install.sh
> ```

---

> **📌 Para este tutorial (K3D Local):**
> Usaremos **mkcert** para ter certificados confiáveis (cadeado verde 🔒) localmente.
> Isso é essencial para testes E2E e simular o ambiente de produção com fidelidade.
>
> **Estratégia por ambiente:**
> - **K3D Local (este tutorial)** → **mkcert** 🔒 (você terá HTTPS de verdade!)
> - **Staging/Homologação** → Let's Encrypt Staging
> - **Produção** → Let's Encrypt Production ou Vault (Fase 2)

---

## 📚 Conceitos Fundamentais de Certificados

Antes de instalar, vamos entender **como certificados funcionam** de forma simples.

### 🔑 1. O que é HTTPS e por que preciso dele?

**HTTP** = Protocolo para sites (não seguro, dados em texto puro)
**HTTPS** = HTTP + TLS (seguro, dados criptografados)

Quando você acessa `https://google.com`:
1. Seu navegador **pede** o certificado do servidor
2. O servidor **envia** seu certificado
3. O navegador **verifica** se confia no certificado
4. Se confiar → Cadeado verde 🔒, conexão segura
5. Se não confiar → ⚠️ "Conexão não é segura"

### 🏢 2. O que é uma CA (Certificate Authority)?

**CA (Autoridade Certificadora)** = Entidade confiável que **assina certificados**

Pense em uma CA como um **cartório digital**:

```
Documento físico          →  Certificado digital
Cartório reconhece firma  →  CA assina certificado
Selo do cartório          →  Assinatura digital da CA
```

**Exemplos de CAs conhecidas:**
- **Let's Encrypt** → CA grátis e automática (usada na produção)
- **DigiCert, GlobalSign** → CAs comerciais (pagas)
- **mkcert** → CA **local** criada por você (para dev)

### 🔐 3. Como funciona a cadeia de confiança?

```
┌─────────────────────────────────────────────────────┐
│  Root CA (CA Raiz)                                  │
│  Exemplo: Let's Encrypt Root CA                     │
│  ↓ assina                                           │
│  Intermediate CA (CA Intermediária)                 │
│  Exemplo: Let's Encrypt Authority X3                │
│  ↓ assina                                           │
│  Seu Certificado                                    │
│  Exemplo: api.mystore.com                           │
└─────────────────────────────────────────────────────┘
```

**Seu navegador confia na Root CA** → Logo confia em tudo que ela assinou!

### 🏠 4. mkcert - CA Local para Desenvolvimento

**mkcert** cria uma CA **local** no seu computador:

```
1. Instala mkcert
   ↓
2. mkcert cria uma Root CA local
   Nome: "mkcert [seu-computador]"
   ↓
3. mkcert instala essa CA no sistema
   Windows: Trust Store
   macOS: Keychain
   Linux: NSS/ca-certificates
   ↓
4. Navegadores passam a confiar nessa CA
   ↓
5. mkcert cria certificados assinados por essa CA
   ↓
6. Navegador vê: "Certificado assinado por CA confiável" ✅
   Resultado: CADEADO VERDE 🔒
```

**Por isso mkcert é mágico:**
- Certificados **self-signed** → Navegador NÃO confia (⚠️ não seguro)
- Certificados **mkcert** → Navegador confia (🔒 cadeado verde)

Ambos são locais, mas mkcert instala a CA no sistema!

### 🤝 5. Como cert-manager se encaixa nisso?

**cert-manager** = Automação de certificados no Kubernetes

```
┌─────────────────────────────────────────────────────┐
│  Você define: "Preciso de certificado para api.x"  │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  cert-manager escolhe uma CA (ClusterIssuer):       │
│  - mkcert CA (dev local)                            │
│  - Let's Encrypt (produção)                         │
│  - Vault PKI (enterprise)                           │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  cert-manager cria o certificado                    │
│  - Gera par de chaves (pública/privada)             │
│  - Envia CSR (Certificate Signing Request) para CA  │
│  - CA assina e retorna certificado                  │
│  - cert-manager armazena em Kubernetes Secret       │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  Seus serviços usam o certificado automaticamente   │
│  Kong, Istio, Ingress → Leem do Secret              │
└─────────────────────────────────────────────────────┘
```

### 📦 6. Estrutura de um Certificado

Um certificado contém:

```yaml
Certificado:
  - Chave Pública (Public Key)      # Criptografa dados
  - Domínio(s) (Subject Alt Names)  # api.mystore.com, *.mystore.com
  - Validade (Not Before/After)     # 2025-01-01 a 2025-04-01
  - Emissor (Issuer)                # Let's Encrypt, mkcert, etc
  - Assinatura Digital              # Prova que CA assinou isso

Secret (armazenado separadamente):
  - Chave Privada (Private Key)     # Descriptografa dados (SEGREDO!)
```

**Analogia:**
- **Certificado** = Seu CPF (pode mostrar para todos)
- **Chave Privada** = Senha do banco (NUNCA compartilhe!)

### 🎯 7. Fluxo Completo no MyStore

```
┌─────────────────────────────────────────────────────┐
│  1. Você instala mkcert no seu computador           │
│     → mkcert cria CA local e instala no sistema     │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  2. Você roda: ./install.sh                         │
│     → Instala cert-manager no cluster               │
│     → Configura ClusterIssuer com CA do mkcert      │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  3. Você cria um Certificate YAML:                  │
│     "Quero certificado para api.mystore.local"      │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  4. cert-manager usa mkcert CA para assinar         │
│     → Cria certificado                              │
│     → Armazena em Secret "api-tls"                  │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  5. Kong Gateway usa Secret "api-tls"               │
│     → Serve HTTPS com certificado válido            │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  6. Você acessa: https://api.mystore.local          │
│     → Navegador vê: "CA confiável" ✅               │
│     → CADEADO VERDE 🔒                              │
└─────────────────────────────────────────────────────┘
```

### 🆚 8. Comparação: mkcert vs Let's Encrypt vs Self-Signed

| Aspecto | mkcert 🔒 | Let's Encrypt 🌐 | Self-Signed ⚡ |
|---------|-----------|------------------|----------------|
| **CA** | Local (você) | Pública (Let's Encrypt) | Você mesmo |
| **Confiança** | Seu computador | Internet toda | Ninguém |
| **Cadeado verde** | ✅ Sim | ✅ Sim | ❌ Não |
| **Funciona offline** | ✅ Sim | ❌ Não | ✅ Sim |
| **Requer domínio público** | ❌ Não | ✅ Sim | ❌ Não |
| **Suporta wildcard** | ✅ Sim | ✅ Sim (DNS-01) | ✅ Sim |
| **Ideal para** | Dev local + E2E | Staging/Prod | Testes rápidos |

### 🌟 9. Por que usar Certificado Wildcard em Dev Local?

**Certificado Wildcard** = Um certificado que cobre `*.mystore.local` (todos os subdomínios)

**Vantagens:**

1. **1 Certificado para Tudo** 🎯
   ```
   *.mystore.local cobre:
   ✅ api.mystore.local
   ✅ app.mystore.local
   ✅ admin.mystore.local
   ✅ grafana.mystore.local
   ✅ qualquer-coisa.mystore.local
   ```

2. **Menos Configuração** ⚡
   - Não precisa criar 1 certificado por serviço
   - 1 Secret (`mystore-wildcard-tls`) usado em todos os Ingress/Gateway

3. **Mais Realista** 🏭
   - Produção geralmente usa wildcards
   - Simula o ambiente real com fidelidade

4. **Renovação Única** 🔄
   - cert-manager renova apenas 1 certificado
   - Todos os serviços atualizam automaticamente

**Exemplo de uso:**
```yaml
# Kong Ingress para API
spec:
  tls:
  - hosts:
    - api.mystore.local
    secretName: mystore-wildcard-tls  # ← Mesmo Secret!

# Kong Ingress para App
spec:
  tls:
  - hosts:
    - app.mystore.local
    secretName: mystore-wildcard-tls  # ← Mesmo Secret!

# Kong Ingress para Admin
spec:
  tls:
  - hosts:
    - admin.mystore.local
    secretName: mystore-wildcard-tls  # ← Mesmo Secret!
```

**Único Secret, múltiplos serviços!** 🚀

---

## 📖 cert-manager para Programadores

Agora que você entende os conceitos, vamos ver **o que é cert-manager** e como usá-lo.

### O que é cert-manager?

**cert-manager** = Gerenciador automático de certificados para Kubernetes

Pense como um **package manager** para certificados:

```
npm/nuget        → Gerencia dependências de código
cert-manager     → Gerencia dependências de certificados

npm install      → Baixa pacotes automaticamente
kubectl apply    → Cria certificados automaticamente

package.json     → Lista dependências
Certificate YAML → Lista certificados necessários
```

### O que cert-manager faz automaticamente?

1. ✅ **Cria** certificados quando você pede
2. ✅ **Renova** antes de expirar (30 dias antes, por padrão)
3. ✅ **Armazena** em Kubernetes Secrets
4. ✅ **Valida** domínios (HTTP-01, DNS-01)
5. ✅ **Injeta** em Ingress/Gateway automaticamente

### Principais Recursos do cert-manager

#### 1. **ClusterIssuer** - Fonte de Certificados

ClusterIssuer = "De onde vem os certificados?"

```bash
# Ver issuers disponíveis
kubectl get clusterissuers

# Exemplo de saída:
# NAME                READY   AGE
# mkcert              True    5m    ← Usa mkcert CA (dev local)
# letsencrypt-prod    True    5m    ← Usa Let's Encrypt (prod)
```

**Tipos de ClusterIssuer:**
- **mkcert** → CA local do mkcert (desenvolvimento)
- **selfsigned** → Auto-assinado (testes rápidos)
- **letsencrypt** → Let's Encrypt (staging/prod)
- **vault** → Vault PKI (enterprise)

#### 2. **Certificate** - O Certificado que Você Pede

Certificate = "Certificado que eu preciso"

```yaml
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: api-mystore-cert
spec:
  secretName: api-mystore-tls    # Nome do Secret que será criado
  issuerRef:
    name: mkcert                  # Usa mkcert CA
    kind: ClusterIssuer
  dnsNames:
    - api.mystore.local           # Domínios do certificado
    - "*.api.mystore.local"       # Wildcard também funciona!
```

```bash
# Ver certificados
kubectl get certificates -A

# Ver detalhes
kubectl describe certificate api-mystore-cert
```

#### 3. **Secret** - Onde o Certificado é Armazenado

Depois que cert-manager cria o certificado, ele armazena em um **Secret**:

```bash
# Ver secrets com certificados
kubectl get secrets -A | grep tls

# Ver conteúdo do certificado
kubectl get secret api-mystore-tls -o jsonpath='{.data.tls\.crt}' | base64 -d | openssl x509 -noout -text
```

O Secret contém:
- `tls.crt` → Certificado (chave pública)
- `tls.key` → Chave privada (NUNCA exponha!)
- `ca.crt` → CA certificate (opcional)

### 🔄 Workflow Completo com mkcert

```bash
# ═══════════════════════════════════════════════════════
# PASSO 1: Instalar mkcert no seu computador (uma vez)
# ═══════════════════════════════════════════════════════

# Windows - Download direto (PowerShell Admin):
# Invoke-WebRequest -Uri https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-windows-amd64.exe -OutFile mkcert.exe
# Move-Item mkcert.exe C:\Windows\System32\mkcert.exe -Force

# OU Windows com winget:
# winget install FiloSottile.mkcert

# macOS:
# brew install mkcert

# ═══════════════════════════════════════════════════════
# PASSO 2: Instalar cert-manager + configurar mkcert
# ═══════════════════════════════════════════════════════
./install.sh
# → O script detecta mkcert e configura automaticamente!
# → ClusterIssuer "mkcert" criado ✅

# ═══════════════════════════════════════════════════════
# PASSO 3: Verificar ClusterIssuer disponível
# ═══════════════════════════════════════════════════════

kubectl get clusterissuers

# Você deve ver:
# - "mkcert" se configurou mkcert no install.sh (RECOMENDADO!)
# - OU crie "selfsigned" se não configurou mkcert:
#   kubectl apply -f clusterissuer-selfsigned.yaml

# ═══════════════════════════════════════════════════════
# PASSO 4: Criar certificado wildcard (RECOMENDADO para dev local)
# ═══════════════════════════════════════════════════════

# 💡 TIP: Use wildcard (*.mystore.local) para cobrir todos os subdomínios!
# Assim você usa o MESMO certificado em api.mystore.local, app.mystore.local, etc.

# Se você tem ClusterIssuer "mkcert" (RECOMENDADO - cadeado verde 🔒):
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: mystore-wildcard-cert
  namespace: default
spec:
  secretName: mystore-wildcard-tls
  issuerRef:
    name: mkcert
    kind: ClusterIssuer
  dnsNames:
    - "*.mystore.local"      # Wildcard para todos subdomínios
    - mystore.local          # Domínio raiz também
EOF

# OU se você criou ClusterIssuer "selfsigned":
# cat <<EOF | kubectl apply -f -
# apiVersion: cert-manager.io/v1
# kind: Certificate
# metadata:
#   name: mystore-wildcard-cert
#   namespace: default
# spec:
#   secretName: mystore-wildcard-tls
#   issuerRef:
#     name: selfsigned
#     kind: ClusterIssuer
#   dnsNames:
#     - "*.mystore.local"
#     - mystore.local
# EOF

# 5. Aguardar certificado ficar pronto (15-30 segundos)
kubectl wait --for=condition=ready certificate mystore-wildcard-cert --timeout=60s

# 6. Agora você pode usar este Secret em TODOS os seus serviços!
# Exemplos:
# - api.mystore.local → usa mystore-wildcard-tls
# - app.mystore.local → usa mystore-wildcard-tls
# - admin.mystore.local → usa mystore-wildcard-tls

# 7. Verificar
kubectl get certificate mystore-wildcard-cert
kubectl get secret mystore-wildcard-tls
# STATUS deve mostrar "True" na coluna READY
```

### Diferenças vs Desenvolvimento "Normal"

| Desenvolvimento Local             | Com cert-manager                          |
|----------------------------------|-------------------------------------------|
| HTTP sem HTTPS                   | HTTPS automático em tudo                  |
| Certificados auto-assinados      | Certificados válidos (Let's Encrypt)      |
| Aviso "Não seguro" no navegador  | Cadeado verde 🔒                          |
| Renovação manual                 | Renovação automática                      |
| Configurar 1 certificado por vez | Configurar centenas de forma declarativa  |

### Por que vale a pena aprender?

1. **Produção exige HTTPS** (lei LGPD, PCI-DSS, compliance)
2. **Let's Encrypt é grátis** mas expira em 90 dias → renovação automática
3. **Service Mesh (Istio)** usa mTLS → cert-manager cria certs para cada serviço
4. **Zero Trust** → tudo é criptografado, até tráfego interno
5. **Não quebra em produção** → nunca mais certificado expirado às 3h da manhã

### Não se assuste!

Você **não precisa** entender criptografia ou PKI. O cert-manager faz tudo:

- `kubectl apply -f certificate.yaml` → Pedir certificado
- `kubectl get certificates` → Ver status
- `kubectl describe certificate <name>` → Debug se der erro

O resto é automático!

---

## 📊 Qual ClusterIssuer Escolher? (Comparação Rápida)

| Característica | mkcert 🔒 | Self-Signed ⚡ | Let's Encrypt 🌐 |
|----------------|-----------|----------------|------------------|
| **Cadeado verde no navegador** | ✅ Sim | ❌ Não | ✅ Sim |
| **Funciona offline (K3D local)** | ✅ Sim | ✅ Sim | ❌ Não |
| **E2E tests sem flags especiais** | ✅ Sim | ❌ Não | ✅ Sim |
| **Setup** | Fácil (1 comando) | Muito fácil | Complexo |
| **Requer domínio público** | ❌ Não | ❌ Não | ✅ Sim |
| **Requer cluster na internet** | ❌ Não | ❌ Não | ✅ Sim |
| **Simula produção 100%** | ✅ Sim | ❌ Não | ✅ Sim |
| **Wildcards (`*.domain.com`)** | ✅ Sim | ✅ Sim | ⚠️ DNS-01 |
| **Quando usar** | **K3D Local + E2E** | K3D sem E2E | Staging/Prod |

**🎯 Recomendação para este tutorial:**
- **Tem 5 minutos?** → Use **mkcert** 🔒 (melhor experiência)
- **Pressa?** → Use **Self-Signed** ⚡ (mais rápido, mas sem cadeado verde)
- **Produção?** → Use **Let's Encrypt** 🌐 (só funciona com domínio público)

---

## ⚡ TL;DR - Início Rápido

### Fluxo Automático com install.sh

```
┌─────────────────────────────────────────────────────────────┐
│  Você:  ./install.sh                                        │
└─────────────────────────┬───────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│  Script instala cert-manager via Helm                       │
│  ✅ Controller, Webhook, CAInjector                         │
└─────────────────────────┬───────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│  Script detecta: Você tem mkcert instalado?                 │
└─────────┬─────────────────────────────┬─────────────────────┘
          │ SIM                         │ NÃO
          ▼                             ▼
┌─────────────────────────┐   ┌─────────────────────────────┐
│ "Quer configurar        │   │ "Mostra como instalar       │
│  mkcert? (Y/n)"         │   │  mkcert"                    │
│                         │   │                             │
│ [Y] → Configura         │   │ "Quer self-signed? (Y/n)"   │
│       automaticamente   │   │                             │
│       🔒 Cadeado verde! │   │ [Y] → Cria self-signed      │
│                         │   │       ⚡ Rápido mas sem     │
│ [n] → Cria self-signed  │   │          cadeado verde      │
└─────────────────────────┘   └─────────────────────────────┘
          │                             │
          └─────────────┬───────────────┘
                        ▼
┌─────────────────────────────────────────────────────────────┐
│  ✅ PRONTO!                                                 │
│  - cert-manager rodando                                     │
│  - ClusterIssuer configurado                                │
│  - Próximos passos mostrados na tela                        │
└─────────────────────────────────────────────────────────────┘
```

### Comandos

```bash
# 1. (OPCIONAL) Instalar mkcert ANTES para melhor experiência 🔒

# Windows - Download direto (PowerShell Admin):
# Invoke-WebRequest -Uri https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-windows-amd64.exe -OutFile mkcert.exe
# Move-Item mkcert.exe C:\Windows\System32\mkcert.exe -Force

# OU winget:
# winget install FiloSottile.mkcert

# macOS:
# brew install mkcert

# 2. Rodar o instalador
cd infra/cert-manager
./install.sh

# 3. Criar ClusterIssuer (IMPORTANTE: escolha uma opção)

# Opção A: Self-signed (recomendado se não instalou mkcert)
kubectl apply -f clusterissuer-selfsigned.yaml

# OU Opção B: mkcert (se seguiu o passo 1 e configurou no install.sh)
# (já criado automaticamente pelo install.sh)

# 4. Verificar ClusterIssuers disponíveis
kubectl get clusterissuers

# 5. Testar criação de certificado
# Com self-signed:
kubectl apply -f example-certificate.yaml
# OU com mkcert:
kubectl apply -f example-certificate-mkcert.yaml

# 6. Aguardar certificado ficar pronto
kubectl wait --for=condition=ready certificate -l app.kubernetes.io/part-of=mystore-platform --timeout=60s

# 7. Verificar
kubectl get certificates
```

**Resultado:**
- ✅ cert-manager instalado e rodando
- ✅ ClusterIssuer configurado (self-signed ou mkcert)
- ✅ Pronto para emitir certificados automaticamente!

---

## 📁 Estrutura de Arquivos

```
infra/cert-manager/
├── 📄 README.md                                    ← Você está aqui!
├── 📄 values.yaml                                  ← Configuração Helm do cert-manager
│
├── 🔧 Scripts de Instalação
│   ├── install.sh                                  ← Script principal (AUTOMÁTICO!)
│   ├── uninstall.sh                                ← Remove tudo
│   └── setup-mkcert.sh                             ← Configura mkcert (chamado pelo install.sh)
│
├── 🔒 ClusterIssuers (Escolha um)
│   ├── clusterissuer-mkcert.yaml                  ← 🔒 mkcert (RECOMENDADO para K3D)
│   ├── clusterissuer-selfsigned.yaml              ← ⚡ Self-signed (alternativa)
│   ├── clusterissuer-letsencrypt-staging.yaml     ← 🌐 Let's Encrypt Staging
│   └── clusterissuer-letsencrypt-production.yaml  ← 🌐 Let's Encrypt Production
│
└── 📝 Exemplos e Testes
    ├── example-wildcard-certificate.yaml          ← ⭐ RECOMENDADO: Wildcard com self-signed
    ├── example-wildcard-certificate-mkcert.yaml   ← ⭐ RECOMENDADO: Wildcard com mkcert
    ├── example-certificate-mkcert.yaml            ← Exemplo single domain com mkcert
    └── example-certificate.yaml                   ← Exemplo single domain com self-signed
```

**Qual arquivo usar?**

| Arquivo | Quando Usar |
|---------|-------------|
| `install.sh` | **SEMPRE** - É o script principal! |
| `clusterissuer-selfsigned.yaml` | **SEMPRE** - Crie após install.sh (se não usar mkcert) |
| `clusterissuer-mkcert.yaml` | Se instalou mkcert (cadeado verde 🔒) |
| `clusterissuer-letsencrypt-*.yaml` | Staging/Produção com domínio público |
| `example-wildcard-certificate.yaml` | ⭐ **DEV LOCAL** - 1 cert para todos serviços! |
| `example-wildcard-certificate-mkcert.yaml` | ⭐ **DEV LOCAL + E2E** - Wildcard confiável |
| `example-certificate.yaml` | Exemplo single domain (menos prático) |
| `example-certificate-mkcert.yaml` | Exemplo single domain com mkcert (menos prático) |

---

## 📋 Pré-requisitos

### Cluster Kubernetes

Você precisa de um cluster K8s rodando. Se ainda não tem:

```bash
cd ../k3d
./create-cluster.sh
```

### Ferramentas Necessárias

1. **kubectl** (já deve ter do k3d)
   ```bash
   kubectl version --client
   ```

2. **Helm** (v3.x)
   ```bash
   # Windows (Chocolatey)
   choco install kubernetes-helm

   # macOS (Homebrew)
   brew install helm

   # Linux
   curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
   ```

### Verificar Instalação

```bash
kubectl cluster-info
helm version
```

## 🚀 Instalação Rápida (Guia Completo)

### Opção 1: Instalação Automática (Recomendado) 🎯

O script `install.sh` instala o cert-manager:

```bash
# Execute o instalador:
./install.sh

# O script vai:
# 1. ✅ Instalar cert-manager via Helm
# 2. ✅ Detectar se você tem mkcert instalado
# 3. ✅ Perguntar se quer configurar mkcert (recomendado!)
# 4. ✅ Mostrar próximos passos

# IMPORTANTE: Após instalar, você DEVE criar um ClusterIssuer!
```

**Se você já tem mkcert instalado:**
- O script vai detectar e oferecer configurar automaticamente
- Apenas responda "Y" quando perguntado
- O ClusterIssuer "mkcert" será criado automaticamente
- Pronto! Você terá HTTPS com cadeado verde 🔒

**Se NÃO tem mkcert instalado:**
- O script vai mostrar como instalar
- Você precisará criar manualmente o ClusterIssuer self-signed:
  ```bash
  kubectl apply -f clusterissuer-selfsigned.yaml
  ```
- Você pode instalar mkcert depois e rodar `./setup-mkcert.sh`

**⚠️ IMPORTANTE:** Sempre verifique se você tem pelo menos um ClusterIssuer criado:
```bash
kubectl get clusterissuers
# Se vazio, você precisa criar um! Senão os certificados não serão emitidos.
```

### Opção 2: Instalação Manual com mkcert (Passo a Passo) 🔒

```bash
# 1. Instalar mkcert primeiro (se ainda não tiver)

# Windows - Download direto (PowerShell Admin):
Invoke-WebRequest -Uri https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-windows-amd64.exe -OutFile mkcert.exe
Move-Item mkcert.exe C:\Windows\System32\mkcert.exe -Force

# OU Windows com winget:
winget install FiloSottile.mkcert

# OU Windows com scoop:
scoop install mkcert

# macOS (Homebrew)
brew install mkcert

# Linux
wget https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-linux-amd64
chmod +x mkcert-v1.4.4-linux-amd64
sudo mv mkcert-v1.4.4-linux-amd64 /usr/local/bin/mkcert

# 2. Instalar cert-manager
./install.sh
# Quando perguntado, escolha "Y" para configurar mkcert

# 3. Pronto! Testar:
kubectl apply -f example-certificate-mkcert.yaml
kubectl get certificates
```

### Opção 3: Instalação Manual com Self-Signed ⚡

```bash
# 1. Instalar cert-manager
./install.sh
# Quando perguntado, escolha usar self-signed

# 2. OU criar manualmente:
kubectl apply -f clusterissuer-selfsigned.yaml

# 3. Testar:
kubectl apply -f example-certificate.yaml
kubectl get certificates
```

### Instalar cert-manager (detalhado)

```bash
# Linux/macOS
./install.sh

# Windows (Git Bash ou WSL)
bash install.sh

# Ou diretamente com Helm
helm install cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --create-namespace \
  --version v1.16.2 \
  --values values.yaml \
  --wait
```

### Verificar Instalação

```bash
# Verificar pods
kubectl get pods -n cert-manager

# Deve mostrar 3 pods rodando:
# - cert-manager-<hash>
# - cert-manager-cainjector-<hash>
# - cert-manager-webhook-<hash>

# Verificar CRDs (Custom Resource Definitions)
kubectl get crd | grep cert-manager

# Deve mostrar:
# - certificaterequests.cert-manager.io
# - certificates.cert-manager.io
# - clusterissuers.cert-manager.io
# - issuers.cert-manager.io
# - challenges.acme.cert-manager.io
# - orders.acme.cert-manager.io
```

### Desinstalar cert-manager

```bash
# Linux/macOS
./uninstall.sh

# Windows (Git Bash ou WSL)
bash uninstall.sh
```

## 🔧 Configuração

### Componentes Instalados

cert-manager é composto por 3 componentes principais:

#### 1. **cert-manager Controller**
- Orquestra a criação e renovação de certificados
- Monitora recursos `Certificate` e `CertificateRequest`
- Lida com a lógica de validação (HTTP-01, DNS-01)

#### 2. **cert-manager Webhook**
- Valida recursos do cert-manager antes de serem criados
- Previne configurações inválidas
- Necessário para conversão de API versions

#### 3. **cert-manager CAInjector**
- Injeta automaticamente CA bundles em:
  - `ValidatingWebhookConfiguration`
  - `MutatingWebhookConfiguration`
  - `APIService`
  - `CustomResourceDefinition`
- Essencial para integração com outras ferramentas

### Recursos Configurados (values.yaml)

```yaml
# Características principais:
- Instala CRDs automaticamente (installCRDs: true)
- Réplicas: 1 (dev) - Aumente para 3+ em produção
- Prometheus metrics habilitados (porta 9402)
- Recursos otimizados para ambiente dev
- Security context hardened (non-root, read-only filesystem)
```

### Recursos de CPU/Memória

```yaml
# Ambiente DEV (padrão):
resources:
  requests:
    cpu: 10m      # Mínimo: 0.01 CPU
    memory: 32Mi  # Mínimo: 32 MB
  limits:
    cpu: 100m     # Máximo: 0.1 CPU
    memory: 128Mi # Máximo: 128 MB

# Ambiente PROD (recomendado):
resources:
  requests:
    cpu: 100m
    memory: 128Mi
  limits:
    cpu: 500m
    memory: 512Mi
```

## 📚 ClusterIssuers

ClusterIssuers são "fornecedores de certificados". Configure os que você precisa:

### 1. mkcert (Desenvolvimento Local com Cadeado Verde) 🔒 **MELHOR PARA K3D + E2E TESTS**

**Quando usar:** Desenvolvimento local com HTTPS confiável, ideal para testes E2E **← RECOMENDADO!**

```bash
# 1. Instalar mkcert (uma vez)
# Windows (Chocolatey)
choco install mkcert

# macOS (Homebrew)
brew install mkcert

# Linux
wget https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-linux-amd64
chmod +x mkcert-v1.4.4-linux-amd64
sudo mv mkcert-v1.4.4-linux-amd64 /usr/local/bin/mkcert

# 2. Configurar mkcert no cluster
./setup-mkcert.sh
```

**Características:**
- ✅ **Cadeado verde 🔒 no navegador** (certificados confiáveis!)
- ✅ Funciona offline
- ✅ **Perfeito para testes E2E** (Playwright não reclama)
- ✅ Simula exatamente o ambiente de produção
- ✅ Sem rate limits
- ✅ Wildcards funcionam (`*.mystore.local`)
- ✅ **IDEAL PARA ESTE TUTORIAL**

**Por que mkcert é melhor que self-signed?**
- Self-signed → Navegador mostra "não seguro" ⚠️
- mkcert → Navegador mostra "seguro" 🔒 (cadeado verde)
- Testes E2E funcionam sem `--ignore-certificate-errors`
- Simula produção perfeitamente

**Como funciona:**
1. mkcert cria uma CA local
2. Instala a CA no trust store do seu sistema
3. Navegadores confiam em certificados dessa CA
4. cert-manager usa essa CA para emitir certificados

### 2. Self-Signed (Desenvolvimento Local - Básico) ⚡ **ALTERNATIVA SIMPLES**

**Quando usar:** Desenvolvimento local básico, sem necessidade de navegador **← SE NÃO USAR mkcert**

```bash
kubectl apply -f clusterissuer-selfsigned.yaml
```

**Características:**
- ✅ Funciona offline
- ✅ Imediato (sem validação)
- ✅ Mais simples que mkcert
- ❌ Navegador mostra "não seguro" ⚠️
- ❌ Testes E2E podem precisar de flags especiais
- ❌ Não simula produção fielmente

**Quando usar self-signed em vez de mkcert:**
- Você só vai testar via API/curl (sem navegador)
- Não vai rodar testes E2E
- Quer a solução mais simples possível

### 3. Let's Encrypt Staging (Homologação) ⚠️ **NÃO usar no K3D**

**Quando usar:** Ambientes de staging/homologação com domínio real e cluster público

```bash
# Editar email antes:
# sed -i 's/your-email@example.com/seu-email@empresa.com/' clusterissuer-letsencrypt-staging.yaml

kubectl apply -f clusterissuer-letsencrypt-staging.yaml
```

**Características:**
- ✅ Certificado "real" (mas não confiável)
- ✅ Testa integração com Let's Encrypt
- ✅ Rate limits relaxados (para testes)
- ❌ Navegador mostra "não seguro" (CA staging)
- ❌ Requer domínio público e validação

### 4. Let's Encrypt Production (Produção) ⚠️ **NÃO usar no K3D**

**Quando usar:** Produção com domínio real e público e cluster acessível na internet

```bash
# Editar email antes:
# sed -i 's/your-email@example.com/seu-email@empresa.com/' clusterissuer-letsencrypt-production.yaml

kubectl apply -f clusterissuer-letsencrypt-production.yaml
```

**Características:**
- ✅ Certificado válido e confiável
- ✅ Cadeado verde 🔒 no navegador
- ✅ GRÁTIS e renovação automática
- ⚠️ Rate limits estritos (5 por semana)
- ❌ Requer domínio público e validação

### 5. Vault Issuer (Enterprise - Fase 2)

**Quando usar:** Produção enterprise com PKI interna (será configurado na Fase 2)

```bash
# Será criado após instalar Vault (Fase 2)
# kubectl apply -f clusterissuer-vault.yaml
```

**Características:**
- ✅ Certificados de sua PKI interna
- ✅ Controle total da CA
- ✅ Integração com Vault
- ✅ Ideal para mTLS interno
- ❌ Requer Vault configurado

## 🧪 Testando cert-manager

### Teste 1: Certificado Self-Signed

```bash
# Criar ClusterIssuer self-signed
kubectl apply -f clusterissuer-selfsigned.yaml

# Criar um certificado de teste
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: test-selfsigned
  namespace: default
spec:
  secretName: test-selfsigned-tls
  duration: 2160h # 90 dias
  renewBefore: 720h # 30 dias antes
  subject:
    organizations:
      - MyStore
  commonName: test.mystore.local
  isCA: false
  privateKey:
    algorithm: RSA
    encoding: PKCS1
    size: 2048
  usages:
    - server auth
    - client auth
  dnsNames:
    - test.mystore.local
    - "*.test.mystore.local"
  issuerRef:
    name: selfsigned
    kind: ClusterIssuer
EOF

# Aguardar certificado ficar pronto (15-30 segundos)
kubectl wait --for=condition=ready certificate test-selfsigned -n default --timeout=60s

# Verificar
kubectl get certificate test-selfsigned -n default
kubectl describe certificate test-selfsigned -n default

# Ver o Secret criado
kubectl get secret test-selfsigned-tls -n default

# Inspecionar o certificado
kubectl get secret test-selfsigned-tls -n default -o jsonpath='{.data.tls\.crt}' | base64 -d | openssl x509 -noout -text
```

### Teste 2: Certificado Let's Encrypt (Staging)

**IMPORTANTE:** Só funciona se você tiver:
1. Domínio público real (ex: `mystore.com.br`)
2. DNS apontando para seu cluster
3. Cluster acessível da internet (porta 80/443)

```bash
# Para ambiente local k3d, você precisa de um túnel como:
# - ngrok
# - cloudflare tunnel
# - inlets

# Criar ClusterIssuer (editar email antes!)
kubectl apply -f clusterissuer-letsencrypt-staging.yaml

# Criar certificado (trocar domínio!)
cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: test-letsencrypt
  namespace: default
spec:
  secretName: test-letsencrypt-tls
  issuerRef:
    name: letsencrypt-staging
    kind: ClusterIssuer
  dnsNames:
    - seu-dominio.com.br  # <-- TROCAR AQUI!
EOF

# Acompanhar progresso
kubectl describe certificate test-letsencrypt -n default

# Ver challenges (validação HTTP-01 ou DNS-01)
kubectl get challenges -n default
kubectl describe challenge <challenge-name>
```

### Teste 3: Usar Certificado em um Ingress

```bash
# Criar um deployment de teste
kubectl create deployment nginx --image=nginx
kubectl expose deployment nginx --port=80

# Criar Ingress com TLS automático
cat <<EOF | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: nginx-ingress
  namespace: default
  annotations:
    cert-manager.io/cluster-issuer: selfsigned
spec:
  ingressClassName: nginx  # ou "kong" se usar Kong
  tls:
  - hosts:
    - nginx.mystore.local
    secretName: nginx-tls  # cert-manager cria automaticamente!
  rules:
  - host: nginx.mystore.local
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: nginx
            port:
              number: 80
EOF

# cert-manager detecta a annotation e cria o Certificate automaticamente!
kubectl get certificate nginx-tls -n default
```

## 💡 Melhores Práticas para Desenvolvimento Local

### 1. Use Certificado Wildcard ⭐

**Sempre prefira wildcard em ambiente local:**

```bash
# ✅ RECOMENDADO - Wildcard (1 cert para tudo)
kubectl apply -f example-wildcard-certificate.yaml

# ❌ NÃO RECOMENDADO - 1 cert por serviço
# kubectl apply -f api-cert.yaml
# kubectl apply -f app-cert.yaml
# kubectl apply -f admin-cert.yaml
```

**Por quê?**
- Menos configuração
- Mais realista (produção usa wildcards)
- Renovação única e automática
- Compartilha Secret entre todos serviços

### 2. Prefira mkcert quando possível 🔒

Se você vai rodar testes E2E ou acessar pelo navegador:

```bash
# Instalar mkcert uma vez
winget install FiloSottile.mkcert

# Configurar no cluster
./setup-mkcert.sh

# Usar wildcard com mkcert
kubectl apply -f example-wildcard-certificate-mkcert.yaml
```

**Benefícios:**
- ✅ Cadeado verde no navegador
- ✅ Testes E2E funcionam sem flags especiais
- ✅ Simula produção perfeitamente

### 3. Namespace padrão ou dedicado?

Para ambiente local, use `default` namespace para facilitar:

```yaml
metadata:
  name: mystore-wildcard-cert
  namespace: default  # ✅ Simples para dev local
```

Para produção, use namespaces dedicados por ambiente.

### 4. Verifique sempre os ClusterIssuers

```bash
# Antes de criar certificados, sempre verificar:
kubectl get clusterissuers

# Deve mostrar pelo menos um:
# NAME            READY   AGE
# selfsigned      True    5m
# selfsigned-ca   True    5m
```

### 5. Monitore renovações automáticas

cert-manager renova automaticamente, mas é bom verificar:

```bash
# Ver quando expira
kubectl get certificate mystore-wildcard-cert -o jsonpath='{.status.notAfter}'

# Ver quando vai renovar
kubectl get certificate mystore-wildcard-cert -o jsonpath='{.status.renewalTime}'
```

## 🎯 Próximos Passos (FASE 1)

Após instalar cert-manager, seguir a ordem definida em [`mystore-platform-architecture.md`](../mystore-platform-architecture.md):

1. ✅ **k3d** → `cd ../k3d`
2. ✅ **cert-manager** ← Você está aqui
3. ⬜ **harbor** → `cd ../harbor`
4. ⬜ **registry** → `cd ../registry`
5. ⬜ **ci** → `cd ../ci`

## 🔍 Troubleshooting

### Erro: ClusterIssuer não encontrado (Referenced "ClusterIssuer" not found)

**Sintoma:**
```
kubectl wait --for=condition=ready certificate minha-api-cert
error: timed out waiting for the condition on certificates/minha-api-cert
```

**Diagnóstico:**
```bash
# Ver detalhes do certificado
kubectl describe certificate minha-api-cert

# Procurar por mensagens como:
# Message: Referenced "ClusterIssuer" not found: clusterissuer.cert-manager.io "selfsigned" not found

# Listar ClusterIssuers disponíveis
kubectl get clusterissuers
```

**Solução:**

Se o ClusterIssuer `selfsigned` não existir, você precisa criá-lo:

```bash
# Criar o ClusterIssuer self-signed
kubectl apply -f clusterissuer-selfsigned.yaml

# Verificar se foi criado com sucesso
kubectl get clusterissuers

# Aguardar o certificado ficar pronto
kubectl wait --for=condition=ready certificate minha-api-cert --timeout=60s
```

**Alternativa:** Use um ClusterIssuer existente

```bash
# Ver quais ClusterIssuers você tem
kubectl get clusterissuers

# Se você tem "mkcert", pode usar ele
# Edite seu certificado para referenciar o issuer correto:
kubectl edit certificate minha-api-cert

# Mude:
# issuerRef:
#   name: selfsigned  # ← trocar para "mkcert" se disponível
```

### Certificado não fica pronto (READY = False)

```bash
# Ver eventos do Certificate
kubectl describe certificate <nome> -n <namespace>

# Ver CertificateRequest gerado
kubectl get certificaterequest -n <namespace>
kubectl describe certificaterequest <nome> -n <namespace>

# Ver challenges (para Let's Encrypt)
kubectl get challenges -n <namespace>
kubectl describe challenge <nome> -n <namespace>

# Ver logs do cert-manager
kubectl logs -n cert-manager -l app=cert-manager --tail=100
```

### Erro: "Issuer not ready"

```bash
# Verificar ClusterIssuer
kubectl get clusterissuer
kubectl describe clusterissuer <nome>

# Se for Let's Encrypt, verificar se ACME account foi criado
kubectl describe clusterissuer letsencrypt-staging
# Procurar por "ACME account registered"
```

### Let's Encrypt retorna erro 403/404

**Causa comum:** Domínio não aponta para seu cluster ou porta 80/443 não acessível

```bash
# Testar se challenge HTTP-01 é acessível
# cert-manager cria um pod temporário em /.well-known/acme-challenge/

# 1. Ver challenge criado
kubectl get challenges -n <namespace>

# 2. Ver Ingress/Service do challenge
kubectl get ingress -n <namespace>
kubectl get svc -n <namespace>

# 3. Testar acesso externo
curl http://seu-dominio.com/.well-known/acme-challenge/test
```

**Solução para desenvolvimento local:**
- Use `selfsigned` ClusterIssuer
- Ou configure DNS-01 challenge (requer integração com DNS provider)
- Ou use ngrok/cloudflare tunnel para expor cluster

### Webhook não responde

```bash
# Verificar webhook está rodando
kubectl get pods -n cert-manager | grep webhook

# Ver logs do webhook
kubectl logs -n cert-manager -l app=webhook

# Reiniciar webhook se necessário
kubectl rollout restart deployment cert-manager-webhook -n cert-manager
```

### CRDs não foram instaladas

```bash
# Verificar CRDs
kubectl get crd | grep cert-manager

# Se não aparecer, instalar manualmente:
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.16.2/cert-manager.crds.yaml
```

### Rate limit do Let's Encrypt

**Erro:** `too many certificates already issued`

**Causa:** Let's Encrypt Production tem limite de 5 certificados/semana por domínio

**Soluções:**
1. Use `letsencrypt-staging` para testes (rate limits relaxados)
2. Use wildcard certificate (`*.mystore.com`)
3. Aguarde 7 dias para reset do rate limit

## 📚 Referências

- [cert-manager Documentation](https://cert-manager.io/docs/)
- [Let's Encrypt Documentation](https://letsencrypt.org/docs/)
- [Kubernetes TLS Documentation](https://kubernetes.io/docs/concepts/services-networking/ingress/#tls)

## ⚙️ Configuração Avançada

### High Availability (HA)

Para produção, rode múltiplas réplicas:

```yaml
# Editar values.yaml:
replicaCount: 3

webhook:
  replicaCount: 3

cainjector:
  replicaCount: 3
```

### Integração com Prometheus

```yaml
# Editar values.yaml:
prometheus:
  enabled: true
  servicemonitor:
    enabled: true  # Requer Prometheus Operator
```

Acessar métricas:

```bash
kubectl port-forward -n cert-manager svc/cert-manager 9402:9402
curl http://localhost:9402/metrics
```

### DNS-01 Challenge (Cloudflare)

Para validação via DNS (útil para wildcard certs):

```yaml
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-dns01
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: your-email@example.com
    privateKeySecretRef:
      name: letsencrypt-dns01
    solvers:
    - dns01:
        cloudflare:
          email: your-cloudflare-email@example.com
          apiTokenSecretRef:
            name: cloudflare-api-token
            key: api-token
```

### Webhook Customizado

Para integrações avançadas com DNS providers:

```bash
# Exemplo: cert-manager-webhook-cloudflare
helm install cert-manager-webhook-cloudflare \
  --namespace cert-manager \
  --set groupName=acme.mystore.local \
  deploy/cert-manager-webhook-cloudflare
```

## 🧪 Exemplos de Uso Real

### Certificado para Kong Gateway

```yaml
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: kong-gateway-cert
  namespace: kong
spec:
  secretName: kong-gateway-tls
  duration: 2160h
  renewBefore: 720h
  issuerRef:
    name: selfsigned
    kind: ClusterIssuer
  dnsNames:
    - api.mystore.local
    - "*.api.mystore.local"
```

### Certificado para Istio Gateway

```yaml
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: istio-gateway-cert
  namespace: istio-system
spec:
  secretName: istio-gateway-tls
  issuerRef:
    name: selfsigned
    kind: ClusterIssuer
  dnsNames:
    - "*.mystore.local"
  usages:
    - server auth
```

### Certificado Wildcard

```yaml
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: wildcard-mystore
  namespace: default
spec:
  secretName: wildcard-mystore-tls
  issuerRef:
    name: letsencrypt-staging
    kind: ClusterIssuer
  dnsNames:
    - "*.mystore.com.br"
    - "mystore.com.br"
  # Wildcard requer DNS-01 challenge!
```

## 📝 Notas

- cert-manager renova certificados automaticamente quando faltam 1/3 do tempo para expirar
- Let's Encrypt certificates expiram em 90 dias → renovação automática ~30 dias antes
- Para produção, use `letsencrypt-production` (após testar com `staging`)
- Self-signed certificates são apenas para desenvolvimento local
- Vault Issuer será configurado na Fase 2 da arquitetura

### 🔑 Diferença entre ClusterIssuers Self-Signed

Quando você cria o arquivo `clusterissuer-selfsigned.yaml`, três recursos são criados:

1. **`selfsigned`** - ClusterIssuer básico
   - Cria certificados auto-assinados diretamente
   - Cada certificado é assinado por si mesmo
   - Use apenas para testes rápidos

2. **`selfsigned-ca`** (Certificate) - Certificado de CA raiz
   - Certificado CA auto-assinado que dura 10 anos
   - Criado pelo ClusterIssuer `selfsigned`
   - Armazenado no Secret `selfsigned-ca-tls`

3. **`selfsigned-ca`** (ClusterIssuer) - ClusterIssuer baseado em CA
   - Usa o certificado CA acima para assinar outros certificados
   - **RECOMENDADO** para uso geral
   - Todos os certificados são assinados pela mesma CA

**Qual usar?**
```yaml
# ❌ Não recomendado (cada cert é diferente):
issuerRef:
  name: selfsigned
  kind: ClusterIssuer

# ✅ Recomendado (todos assinados pela mesma CA):
issuerRef:
  name: selfsigned-ca
  kind: ClusterIssuer

# ✅ Melhor para dev (cadeado verde no navegador):
issuerRef:
  name: mkcert
  kind: ClusterIssuer
```
