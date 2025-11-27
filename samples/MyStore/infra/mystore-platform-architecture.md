
# 🧱 MyStore Cloud-Ready Platform – Arquitetura, Testes e Ordem de Implementação

Este documento descreve **arquitetura**, **estrutura de diretórios** e **ordem sequencial de implementação** da plataforma MyStore, totalmente baseada em `/samples/MyStore`, com foco em ambiente cloud-ready e suite de testes completa.

---

# 📁 Estrutura de Diretórios – `/samples/MyStore`

```text
/samples/MyStore
  /infra                    # Toda a infraestrutura local cloud-ready (K8s, mesh, observabilidade, etc.)
    /charts                 # Helm charts (Istio, Kong, Cert-Manager etc.)
    /manifests              # Manifests K8s puros
    /argo                   # ArgoCD + Argo Rollouts config
    /consul                 # Configurações do Consul
    /vault                  # Policies, roles, PKI e mounts do Vault
    /harbor                 # Configuração Harbor (Helm ou compose)
    /unleash                # Feature flags service
    /grafana                # Dashboards e datasources
    /litmus                 # Experimentos de chaos (LitmusChaos)
    /schemas                # AsyncAPI + Avro + JSON Schemas
    /cdc                    # Config do Debezium (connectors)
    /migration              # Scripts Flyway (opcional, via CI/CD)
    /k3d                    # Config para cluster local
    /cert-manager           # Issuers ACME + Vault + ClusterIssuer
    /mesh                   # Istio configs (Gateway, VirtualServices, DestinationRules)
    /gateway                # Kong declarative configs + Gateway API
    /registry               # Harbor registry automation e config
    /otel                   # Alloy + pipelines OpenTelemetry
    /ci                     # GitHub Actions templates e scripts

  /apps                     # Aplicações de exemplo (BFF, serviços, front-ends)
    /gateway                # Kong declarative (se quiser separar do infra)
    /bff                    # BFF .NET
    /services               # Microserviços
      /orders
      /customers
      /catalog
      /payments
      ...
    /frontends
      /webapp               # Blazor Web App (server+wasm)
      /admin-dashboard

  /tests                    # Testes automatizados (não é infra, é suíte de qualidade)
    /unit                   # Testes unitários clássicos
    /mutation               # Stryker.NET (mutação)
    /integration            # Testes de integração (Testcontainers, DB, mensageria)
    /contracts              # Pact (contract testing)
    /perf-k6                # Cenários de performance/API com k6
    /e2e-playwright         # Testes end-to-end com Playwright
    /chaos                  # Orquestração de cenários de chaos (chamando Litmus via API/CLI)
```

---

# 🔍 Ferramentas de Teste Adotadas

- **Unit + Integration**
  - xUnit / NUnit / MSTest (a escolher)
  - **Testcontainers.NET** para subir PostgreSQL, MongoDB, Kafka, RabbitMQ, etc.

- **Mutation Testing**
  - **Stryker.NET** para medir a qualidade real dos testes unitários.

- **Contract Testing**
  - **Pact** (pode integrar com broker remoto depois, ex: Pactflow).

- **Performance / Carga / Stress**
  - **k6** para cenários de:
    - carga em APIs/BFF
    - provas de resiliência em endpoints
    - testes em cenários de rollout/canary

- **End-to-End (E2E)**
  - **Playwright**:
    - fluxos completos no front (Blazor)
    - testes cross-browser
    - integração com pipeline

- **Chaos Engineering**
  - **LitmusChaos** (infra) acionado a partir de scripts em `/tests/chaos`, para:
    - falha de nó
    - interrupção de pod
    - network latency
    - quedas de Kafka/Redis/PostgreSQL

---

# 🧭 ORDEM FINAL DE IMPLEMENTAÇÃO

Ordem pensada para aprendizado incremental, sem travar por dependência de algo ainda não configurado.

---

## 🎯 FASE 1 — Fundamentos (Infra mínima) – `/infra`

1. `/infra/k3d` – criar cluster local K3d  
2. `/infra/cert-manager` – instalar Cert-Manager  
3. `/infra/harbor` – instalar Harbor via Helm  
4. `/infra/registry` – automação de push/pull + Cosign  
5. `/infra/ci` – pipeline GitHub Actions → Harbor

> Resultado: ambiente local funcional + registry + certs básicos.

---

## 🎯 FASE 2 — Segurança e Segredos – `/infra`

1. `/infra/vault` – Vault + PKI + Auth Kubernetes  
2. `/infra/cert-manager` – Issuer Vault PKI  
3. `/infra/vault/agent` – Sidecar Injector  
4. `/infra/consul` – KV + service discovery (básico)

> Resultado: PKI interna, segredos automáticos, service discovery básico.

---

## 🎯 FASE 3 — Gateway e Mesh – `/infra`

1. `/infra/mesh` – instalar e configurar Istio  
2. `/infra/gateway` – instalar Kong Gateway (com HTTP/3)  
3. Integrar Kong ↔ Cert-Manager  
4. Integrar Istio ↔ Cert-Manager  
5. Ativar mTLS STRICT

> Resultado: tráfego interno seguro e HTTP/3 externo funcional.

---

## 🎯 FASE 4 — Observabilidade Completa – `/infra`

1. `/infra/otel` – Alloy Collector  
2. `/infra/grafana` – Loki + Tempo + Mimir + Grafana  
3. Instrumentar aplicações .NET com OpenTelemetry  
4. Criar dashboards pré-prontos

> Resultado: logs, métricas e traces completos.

---

## 🎯 FASE 5 — Data & Messaging – `/infra`

1. Subir PostgreSQL (manifest ou helm)  
2. Subir MongoDB  
3. `/infra/cdc` – Kafka + Schema Registry + Debezium  
4. `/infra/manifests` – RabbitMQ  
5. Configurar DLQs (Kafka e Rabbit)

> Resultado: bancos, mensageria e CDC completos.

---

## 🎯 FASE 6 — GitOps + Progressive Delivery – `/infra`

1. `/infra/argo` – ArgoCD  
2. `/infra/argo/rollouts` – Argo Rollouts + Istio  
3. Conectar GitHub Actions → ArgoCD

> Resultado: deploy automatizado, canary, blue/green.

---

## 🎯 FASE 7 — Feature Flags & Experimentos – `/infra` + `/apps`

1. `/infra/unleash` – subir Unleash Server  
2. Integrar **OpenFeature** nos projetos .NET em `/apps`  
3. Configurar A/B testing → Unleash + Kong/Istio routing

> Resultado: experimentação aplicada e rollout controlado.

---

## 🎯 FASE 8 — Suite de Testes Avançados – `/tests` + apps

Nesta fase você monta a suíte de testes completa da MyStore.

### 8.1 – Testes Unitários e Integração

1. Criar `/tests/unit`:
   - Projetos de teste unitário por serviço (ex: `Orders.UnitTests`)  
2. Criar `/tests/integration`:
   - Projetos de integração usando **Testcontainers.NET**  
   - Subir PostgreSQL/Mongo/Kafka/Rabbit localmente nos testes  
3. Integrar estes testes à pipeline do GitHub Actions (`/infra/ci`)

> Resultado: base sólida de unit + integração.

---

### 8.2 – Mutation Testing com Stryker.NET

1. Configurar **Stryker.NET** em `/tests/mutation`:
   - Um projeto por bounded context principal  
   - Integração com testes unitários já criados  
2. Adicionar etapa opcional de Stryker no GitHub Actions:
   - Pode ser em stage separado (ex: nightly)

> Resultado: medição real de qualidade de testes.

---

### 8.3 – Contract Testing com Pact

1. Criar projetos em `/tests/contracts`:
   - Ex.: `BFF.Orders.Contracts`, `Orders.Customers.Contracts`  
2. Definir contracts consumer-driven  
3. Integrar Pact na pipeline:
   - Localmente, usar broker simples ou arquivo  
   - Futuro: integrar com Pactflow

> Resultado: estabilidade de integração entre serviços.

---

### 8.4 – Performance / Carga com k6

1. Criar `/tests/perf-k6`:
   - Scripts k6 (`*.js` ou `*.ts`) focados em:
     - BFF  
     - endpoints críticos (checkout, login, search)  
2. Configurar execuções:
   - Local (dev): smoke perf  
   - Pipeline: smoke perf + eventualmente carga controlada

> Resultado: visibilidade de throughput, latência, percentis, erros.

---

### 8.5 – End-to-End com Playwright

1. Criar `/tests/e2e-playwright`:
   - Projetos Playwright em TypeScript ou C#  
   - Fluxos principais:
     - login  
     - cadastro cliente  
     - criação de pedido  
     - checkout  
2. Integrar com GitHub Actions:
   - Rodar contra ambiente de preview (via Argo Rollouts)

> Resultado: validação ponta-a-ponta em ambiente real.

---

### 8.6 – Chaos Engineering com LitmusChaos

1. Em `/infra/litmus` manter os experimentos de chaos  
2. Em `/tests/chaos` scripts que:
   - Disparam experimentos via API do Litmus  
   - Executam k6 ou Playwright durante o caos  
   - Validam SLOs básicos (ex: latência máxima, erro < X%)

> Resultado: resiliência comprovada sob falhas reais.

---

## 🎯 FASE 9 — Developer Experience – `/infra` + `/backstage` (opcional)

1. Instalar Backstage (se optar)  
2. Criar catálogo de serviços baseado em `/apps`  
3. Templates para novos microserviços  
4. Integração com:
   - Harbor  
   - ArgoCD  
   - Grafana  
   - Pact  
   - AsyncAPI/OpenAPI em `/infra/schemas`

> Resultado: portal de desenvolvedor completo.

---

# 🏁 RESUMO

- Toda infra fica em **`/samples/MyStore/infra`**.  
- Toda aplicação (BFF, serviços, front) fica em **`/samples/MyStore/apps`**.  
- Toda suíte de testes (unit, mutation, integration, perf, e2e, chaos) fica em **`/samples/MyStore/tests`**.  
- A ordem de implementação foi pensada para:
  - Minimizar retrabalho  
  - Permitir aprendizado gradual  
  - Manter a plataforma 100% cloud-ready desde o início  

Este documento é o **guia mestre** para construir a plataforma MyStore ao longo dos próximos meses.
