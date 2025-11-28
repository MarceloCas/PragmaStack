# 📚 Documentação PragmaStack

Bem-vindo à documentação completa do **PragmaStack**! Aqui você encontrará metodologias, boas práticas, guias detalhados e recomendações para desenvolver software de qualidade.

---

## 🗂️ Estrutura da Documentação

A documentação está organizada em categorias que facilitam a navegação e compreensão dos conteúdos:

### 📦 Pacotes (`/packages`)

Documentação técnica e guias de uso dos pacotes que compõem o PragmaStack.

#### Core (`/packages/core`)

Funcionalidades essenciais e primitivas que formam a base do framework.

- **Time Providers** - Controle preciso sobre a fonte de tempo em sua aplicação
  - [`CustomTimeProvider`](./packages/core/time-providers/custom-time-provider.md) - Implementação personalizável de TimeProvider para testes e simulações
  - **Time Providers** - Controle preciso sobre a fonte de tempo em sua aplicação
- **Ids** - Geração de IDs únicos e consistentes
  - [`Id`](./packages/core/ids/id.md) - Implementação de IDs com suporte a diferentes estratégias de geração
- **Registry Versions** - Geração de versões de registros
  - [`RegistryVersion`](./packages/core/registry-versions/registry-version.md) - Implementação de geração de versão de registro visando performance e solução de problemas comuns como versões monotônicas para optimistic locking

### 🔬 Metodologias (`/methodologies`)

Princípios, metodologias e boas práticas para o desenvolvimento de software de qualidade.

#### Benchmarking (`/methodologies/benchmarking`)

Guias completos sobre como medir e avaliar o desempenho de software com precisão.

- [`Princípios de Benchmarking`](./methodologies/benchmarking/benchmarking-principles.md) - Fundamentos e práticas recomendadas para benchmarks confiáveis
  - Como executar benchmarks corretamente
  - Como interpretar resultados
  - Tabela de referência de métricas

- [`Armadilhas Comuns em Benchmarking`](./methodologies/benchmarking/benchmarking-pitfalls.md) - Erros frequentes a evitar
  - Ambiente de teste inconsistente
  - Falta de repetição
  - Ignorar o período de aquecimento (warm-up)
  - Foco excessivo em micro-otimizações
  - E muito mais...

---

## 🎯 Como Usar Esta Documentação

### Para Iniciantes
1. Comece pelos **Princípios de Benchmarking** para entender conceitos fundamentais
2. Leia sobre as **Armadilhas Comuns** para evitar erros desde o início
3. Explore os pacotes conforme sua necessidade

### Para Desenvolvedores Experientes
1. Consulte a documentação específica dos pacotes que deseja utilizar
2. Aprofunde-se nas metodologias que se alinhem com seus desafios
3. Use como referência para implementar boas práticas em seus projetos

### Para Arquitetos e Tech Leads
1. Revise as metodologias para estabelecer padrões na equipe
2. Use os princípios documentados como base para decisões arquiteturais
3. Compartilhe com o time os guias relevantes para melhorar a qualidade geral

---

## 📖 Estrutura de Cada Documento

Cada documento de metodologia ou prática segue um padrão consistente:

| Seção | Conteúdo |
|-------|----------|
| 📍 **CONTEXTO** | A situação ou cenário em que a prática é aplicada |
| 🔴 **PROBLEMAS** | Os desafios e problemas que a prática resolve |
| 💚 **BENEFÍCIOS** | Os ganhos e vantagens obtidas |
| ⚖️ **TRADE-OFFS** | Os compromissos e limitações envolvidas |
| 💡 **EXEMPLOS** | Código e casos de uso práticos |
| 📊 **RESULTADOS** | Dados e benchmarks que validam a abordagem |

---

## 🚀 Navegação Rápida

### Quero melhorar o desempenho da minha aplicação
→ Leia [`Princípios de Benchmarking`](./methodologies/benchmarking/benchmarking-principles.md)

### Quero aprender sobre o CustomTimeProvider
→ Leia [`CustomTimeProvider`](./packages/core/time-providers/custom-time-provider.md)

### Quero evitar erros comuns em benchmarking
→ Leia [`Armadilhas Comuns em Benchmarking`](./methodologies/benchmarking/benchmarking-pitfalls.md)

### Preciso entender melhor uma métrica específica
→ Consulte a tabela de referência em [`Princípios de Benchmarking`](./methodologies/benchmarking/benchmarking-principles.md#legenda-de-benchmarking---tabela-de-referência)

---

## ⚠️ Importante

> 📝 Cada documento nesta base de conhecimento foi desenvolvido com rigor e baseado em experiência prática. No entanto, **nenhum deles deve ser tratado como verdade absoluta**.
>
> As recomendações aqui apresentadas:
> - Foram validadas em contextos específicos
> - Podem não ser aplicáveis a todas as situações
> - Devem ser adaptadas conforme as necessidades do seu projeto
> - Foram escritas por um desenvolvedor, não por um especialista

Use esses documentos como **inspiração e ponto de partida**, não como regras imutáveis.

---

## 📝 Contribuindo

Você encontrou um erro? Tem sugestões de melhoria? Quer adicionar conteúdo?

1. Abra uma [issue](https://github.com/MarceloCas/PragmaStack/issues) descrevendo a sugestão
2. Faça um fork e submeta um pull request com suas mudanças
3. Mantenha a consistência com a estrutura e estilo de documentação existente

---

## 🔗 Links Relacionados

- 🏠 [Voltar ao README Principal](../README.md)

---

<div align="center">

**Desenvolvido por Marcelo Castelo Branco**

</div>
