# 🎯 MyStore k3d Cluster

Configuração local do cluster Kubernetes usando k3d para desenvolvimento da plataforma MyStore.

## 📖 Introdução ao k3d para Programadores

### O que é k3d e por que você precisa dele?

Como programador, você está acostumado a rodar aplicações localmente (ex: `dotnet run`, `npm start`). Mas aplicações modernas em **produção** rodam em **Kubernetes** (K8s), que é um orquestrador de containers.

**k3d** é uma ferramenta que cria um **cluster Kubernetes completo** na sua máquina em segundos, dentro do Docker. É como ter um "mini datacenter" local para testar suas aplicações como elas rodarão em produção.

### Analogia para Programadores

Pense assim:

```
Docker          → Como rodar 1 aplicação em container
Docker Compose  → Como rodar múltiplas aplicações conectadas
Kubernetes      → Como rodar centenas de aplicações com alta disponibilidade
k3d             → Kubernetes local para desenvolvedores
```

### Principais Componentes (o que você vai usar)

#### 1. **Cluster** (seu ambiente completo)
O cluster é o "computador virtual" onde suas aplicações rodam.

```bash
# Criar cluster
k3d cluster create mystore-cluster

# Listar clusters
k3d cluster list
```

#### 2. **Nodes** (máquinas dentro do cluster)
São como "servidores virtuais" dentro do cluster. No nosso caso:
- **1 Server Node** = O "cérebro" que controla tudo
- **3 Agent Nodes** = Os "trabalhadores" que executam suas aplicações

```bash
# Ver nodes (são containers Docker!)
kubectl get nodes
docker ps  # Você verá 4 containers: 1 server + 3 agents
```

#### 3. **Pods** (suas aplicações rodando)
Um Pod é a menor unidade no Kubernetes. É basicamente **um ou mais containers rodando juntos**.

```bash
# Ver todos os pods
kubectl get pods --all-namespaces

# Criar um pod de exemplo
kubectl run nginx --image=nginx

# Ver seus pods
kubectl get pods
```

#### 4. **Services** (como acessar aplicações)
Um Service é como um "DNS interno" que permite outras aplicações encontrarem a sua.

```bash
# Expor uma aplicação
kubectl expose pod nginx --port=80 --type=LoadBalancer

# Acessar
kubectl get svc nginx  # Pega a porta
curl localhost:<porta>
```

#### 5. **Namespaces** (organização lógica)
Namespaces são como "pastas" para organizar recursos. Evita conflitos entre aplicações.

```bash
# Ver namespaces
kubectl get ns

# Criar namespace
kubectl create namespace mystore-dev

# Usar namespace
kubectl get pods -n mystore-dev
```

### Como k3d funciona (Simplificado)

```
┌─────────────────────────────────────────────────┐
│  Sua Máquina (Windows/Mac/Linux)                │
│                                                 │
│  ┌───────────────────────────────────────────┐ │
│  │  Docker Desktop                           │ │
│  │                                           │ │
│  │  ┌─────────────────────────────────────┐ │ │
│  │  │  k3d Cluster (containers)           │ │ │
│  │  │                                     │ │ │
│  │  │  [Server Node]                      │ │ │
│  │  │       ↓                             │ │ │
│  │  │  [Agent 1] [Agent 2] [Agent 3]     │ │ │
│  │  │                                     │ │ │
│  │  │  Suas apps rodam aqui como Pods    │ │ │
│  │  └─────────────────────────────────────┘ │ │
│  └───────────────────────────────────────────┘ │
└─────────────────────────────────────────────────┘
```

**Em resumo:**
1. k3d cria containers Docker que **simulam máquinas**
2. Dentro desses containers, roda **Kubernetes completo**
3. Você faz deploy das **suas aplicações** como faria em produção

### Comandos Essenciais (tudo que você precisa)

```bash
# === GERENCIAR CLUSTER ===
k3d cluster create mystore-cluster    # Criar
k3d cluster delete mystore-cluster    # Deletar
k3d cluster list                      # Listar

# === VER RECURSOS ===
kubectl get nodes                     # Máquinas do cluster
kubectl get pods                      # Aplicações rodando
kubectl get services                  # Como acessar aplicações
kubectl get namespaces                # "Pastas" de organização

# === FAZER DEPLOY ===
kubectl create deployment api --image=minha-api:latest
kubectl expose deployment api --port=80 --type=LoadBalancer

# === DEBUG ===
kubectl logs <pod-name>               # Ver logs da aplicação
kubectl describe pod <pod-name>       # Detalhes do pod
kubectl exec -it <pod-name> -- bash   # "Entrar" no container
```

### Workflow Típico para Programadores

```bash
# 1. Criar cluster (1x, no início do dia/projeto)
./create-cluster.sh

# 2. Desenvolver sua aplicação normalmente
cd ../../apps/services/orders
dotnet build
docker build -t localhost:5000/orders:latest .

# 3. Publicar no registry local
docker push localhost:5000/orders:latest

# 4. Fazer deploy no cluster
kubectl create deployment orders --image=localhost:5000/orders:latest
kubectl expose deployment orders --port=80

# 5. Testar
kubectl get svc orders  # Pega a porta
curl localhost:<porta>/health

# 6. Ver logs (se algo der errado)
kubectl logs -l app=orders --follow

# 7. Deletar cluster (no final, se quiser limpar tudo)
./delete-cluster.sh
```

### Diferenças vs Desenvolvimento "Normal"

| Desenvolvimento Local         | Com k3d/Kubernetes                        |
|------------------------------|-------------------------------------------|
| `dotnet run`                 | `kubectl create deployment`               |
| `localhost:5000`             | `kubectl get svc` → porta dinâmica        |
| 1 aplicação por vez          | Dezenas de apps rodando simultaneamente   |
| Reiniciar manualmente        | Kubernetes reinicia automaticamente       |
| Sem load balancer           | Load balancer automático entre réplicas   |
| Variáveis de ambiente `.env` | ConfigMaps e Secrets                      |

### Por que vale a pena aprender?

1. **Produção usa Kubernetes** (AWS EKS, Azure AKS, GCP GKE)
2. **Testa problemas reais**: rede, DNS, service discovery
3. **CI/CD realista**: mesmo ambiente local e produção
4. **Aprende DevOps** sem precisar de um cluster caro na nuvem
5. **Resiliência**: simula falhas (chaos engineering)

### Não se assuste!

Você **não precisa** ser expert em Kubernetes. O k3d + os scripts já deixam tudo pronto. Você só vai usar comandos básicos:

- `kubectl get pods` → Ver o que está rodando
- `kubectl logs <pod>` → Ver logs
- `kubectl delete pod <pod>` → Reiniciar aplicação

O resto é automático!

---

## 📋 Pré-requisitos

### Ferramentas Necessárias

1. **Docker Desktop** (ou Docker Engine)
   - Windows/Mac: [Docker Desktop](https://www.docker.com/products/docker-desktop)
   - Linux: Docker Engine + Docker Compose

2. **k3d** (v5.6.0+)
   ```bash
   # Windows (com Chocolatey)
   choco install k3d

   # macOS (com Homebrew)
   brew install k3d

   # Linux
   curl -s https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash
   ```

3. **kubectl**
   ```bash
   # Windows (com bash)
   curl.exe -LO "https://dl.k8s.io/release/v1.34.0/bin/windows/amd64/kubectl.exe"


   # macOS (com Homebrew)
   brew install kubectl

   # Linux
   curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
   sudo install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl
   ```

### Verificar Instalação

```bash
docker --version
k3d --version
kubectl version --client
```

## 🚀 Uso

### Criar o Cluster

```bash
# Linux/macOS
./create-cluster.sh

# Windows (Git Bash ou WSL)
bash create-cluster.sh

# Ou diretamente com k3d
k3d cluster create --config k3d-config.yaml
```

### Verificar o Cluster

```bash
# Listar clusters
k3d cluster list

# Verificar nodes
kubectl get nodes

# Informações do cluster
kubectl cluster-info

# Verificar namespaces
kubectl get ns
```

### Deletar o Cluster

```bash
# Linux/macOS
./delete-cluster.sh

# Windows (Git Bash ou WSL)
bash delete-cluster.sh

# Ou diretamente
k3d cluster delete mystore-cluster
```

## 🔧 Configuração do Cluster

### Especificações (k3d-config.yaml)

- **Nome**: `mystore-cluster`
- **Server nodes**: 1
- **Agent nodes**: 3
- **Kubernetes version**: v1.28.5
- **Registry local**: `localhost:5000`

### Portas Expostas

| Porta | Protocolo | Uso                          |
|-------|-----------|------------------------------|
| 80    | HTTP      | Ingress HTTP                 |
| 443   | HTTPS     | Ingress HTTPS                |
| 8080  | HTTP      | Gateway/Admin (Kong)         |
| 5000  | HTTP      | Container Registry (interno) |

### Recursos Desabilitados

Por padrão, o cluster k3d vem com alguns recursos que foram **desabilitados** para usar alternativas:

- **Traefik**: Desabilitado (usaremos Kong + Istio)
- **ServiceLB**: Desabilitado (k3d já fornece loadbalancer)

### Volume Persistente

- Path no host: `/tmp/k3dvol`
- Path no container: `/tmp/k3dvol`
- Disponível em: todos os nodes

## 🎯 Próximos Passos (FASE 1)

Após criar o cluster, seguir a ordem definida em [`mystore-platform-architecture.md`](../mystore-platform-architecture.md):

1. ✅ **k3d** ← Você está aqui
2. ⬜ **cert-manager** → `cd ../cert-manager`
3. ⬜ **harbor** → `cd ../harbor`
4. ⬜ **registry** → `cd ../registry`
5. ⬜ **ci** → `cd ../ci`

## 🔍 Troubleshooting

### Docker não está rodando

```bash
# Verificar status do Docker
docker ps

# Se não funcionar, iniciar Docker Desktop ou Docker daemon
```

### Cluster não inicia

```bash
# Verificar logs
k3d cluster list
docker ps -a | grep k3d

# Deletar e recriar
k3d cluster delete mystore-cluster
./create-cluster.sh
```

### kubectl não conecta

```bash
# Verificar contexto
kubectl config current-context

# Deve mostrar: k3d-mystore-cluster

# Se não, configurar manualmente
k3d kubeconfig merge mystore-cluster --kubeconfig-switch-context
```

### Porta já em uso

Se as portas 80, 443, 8080 ou 5000 já estiverem em uso:

1. Parar os serviços que estão usando essas portas
2. Ou editar `k3d-config.yaml` para usar portas diferentes

```yaml
ports:
  - port: 8080:80      # Mapeia porta 8080 do host para 80 do cluster
  - port: 8443:443     # Mapeia porta 8443 do host para 443 do cluster
```

## 📚 Referências

- [k3d Documentation](https://k3d.io/)
- [k3s Documentation](https://docs.k3s.io/)
- [Kubernetes Documentation](https://kubernetes.io/docs/home/)

## ⚙️ Configuração Avançada

### Adicionar mais nodes

Editar `k3d-config.yaml`:

```yaml
servers: 3    # Múltiplos control-plane para HA
agents: 5     # Mais worker nodes
```

### Usar versão específica do Kubernetes

Editar `k3d-config.yaml`:

```yaml
image: rancher/k3s:v1.29.0-k3s1  # Versão mais recente
```

### Registry Personalizado

O cluster já vem com um registry local em `localhost:5000`, mas você pode configurar registries adicionais:

```yaml
registries:
  config: |
    mirrors:
      "docker.io":
        endpoint:
          - https://registry-1.docker.io
      "ghcr.io":
        endpoint:
          - https://ghcr.io
```

## 🧪 Testando o Cluster

### Deploy de teste

```bash
# Criar um deployment de teste
kubectl create deployment nginx --image=nginx

# Expor como serviço
kubectl expose deployment nginx --port=80 --type=LoadBalancer

# Verificar
kubectl get svc nginx
curl localhost:<porta-mostrada>

# Limpar
kubectl delete svc nginx
kubectl delete deployment nginx
```

### Verificar registry local

```bash
# Tag uma imagem para o registry local
docker pull nginx:alpine
docker tag nginx:alpine localhost:5000/nginx:alpine

# Push para o registry
docker push localhost:5000/nginx:alpine

# Usar no cluster
kubectl create deployment nginx-local --image=localhost:5000/nginx:alpine
```

## 📝 Notas

- O cluster é **efêmero**: ao deletar, todos os dados são perdidos
- Para ambientes de desenvolvimento, isso é intencional (infraestrutura como código)
- Dados persistentes devem usar PersistentVolumes ou soluções externas (MinIO, bancos de dados, etc.)
- O registry local (`localhost:5000`) também é efêmero e será substituído pelo Harbor (FASE 1.3)
