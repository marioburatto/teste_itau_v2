# Sistema de Compra Programada de Acoes - Itau Corretora

## Visao Geral

Sistema de compra programada de acoes que permite clientes aderirem a um plano de investimento recorrente e automatizado em uma carteira recomendada de 5 acoes ("Top Five"), com compras consolidadas, distribuicao proporcional e rebalanceamento automatico.

## Arquitetura

O projeto segue **Clean Architecture / DDD** com as seguintes camadas:

```
src/
├── CompraProgramada.Domain          # Entidades, Enums, Interfaces de repositorio
├── CompraProgramada.Application     # DTOs, Servicos de negocio, Interfaces de infraestrutura
├── CompraProgramada.Infrastructure  # EF Core, MySQL, Kafka, COTAHIST parser, Repositorios
└── CompraProgramada.API             # Controllers REST, Swagger, DI configuration
tests/
└── CompraProgramada.Tests           # Testes unitarios e de integracao (xUnit + Moq + FluentAssertions)
```

### Stack Tecnologica

| Componente       | Tecnologia                          |
|------------------|-------------------------------------|
| Backend          | .NET 8 (C#)                         |
| Banco de Dados   | MySQL 8.0 (via Docker)              |
| Mensageria       | Apache Kafka (via Docker)           |
| ORM              | Entity Framework Core 8 (Pomelo)    |
| API              | REST com Swagger/OpenAPI            |
| Cotacoes         | Parser de arquivo COTAHIST da B3    |
| Testes           | xUnit, Moq, FluentAssertions       |
| Containerizacao  | Docker + Docker Compose             |

## Como Executar

### Pre-requisitos

- Docker e Docker Compose instalados

### 1. Subir toda a infraestrutura + API

```bash
docker-compose up --build -d
```

Isso ira iniciar:
- **MySQL** na porta 3306
- **Zookeeper** na porta 2181
- **Kafka** na porta 9092 (interno) / 29092 (externo)
- **API** na porta 8080

### 2. Acessar o Swagger

Abra no navegador: [http://localhost:8080](http://localhost:8080)

### 3. Executar os testes

```bash
docker build -f Dockerfile.tests -t compra-programada-tests .
docker run --rm compra-programada-tests
```

Ou com cobertura:

```bash
docker run --rm -v $(pwd)/testresults:/testresults compra-programada-tests
```

## Endpoints da API

### Cliente

| Metodo | Endpoint                              | Descricao                          |
|--------|---------------------------------------|------------------------------------|
| POST   | `/api/clientes/adesao`                | Aderir ao produto                  |
| POST   | `/api/clientes/{id}/saida`            | Sair do produto                    |
| PUT    | `/api/clientes/{id}/valor-mensal`     | Alterar valor mensal               |
| GET    | `/api/clientes/{id}/carteira`         | Consultar carteira/custodia        |
| GET    | `/api/clientes/{id}/rentabilidade`    | Consultar rentabilidade detalhada  |

### Administracao

| Metodo | Endpoint                              | Descricao                          |
|--------|---------------------------------------|------------------------------------|
| POST   | `/api/admin/cesta`                    | Cadastrar/Alterar cesta Top Five   |
| GET    | `/api/admin/cesta/atual`              | Visualizar cesta atual             |
| GET    | `/api/admin/cesta/historico`          | Historico de cestas                |
| GET    | `/api/admin/conta-master/custodia`    | Consultar custodia master          |

### Motor de Compra

| Metodo | Endpoint                              | Descricao                          |
|--------|---------------------------------------|------------------------------------|
| POST   | `/api/motor/executar-compra`          | Executar compra manualmente        |

## Funcionalidades Implementadas

### Motor de Compra Programada
- Agrupamento de pedidos de clientes ativos
- Calculo de 1/3 do valor mensal por data (dias 5, 15, 25)
- Ajuste para dia util seguinte quando cai em fim de semana
- Calculo consolidado por ativo segundo percentuais da cesta Top Five
- Desconto de saldo residual da custodia master
- Separacao lote padrao (multiplos de 100) vs fracionario (sufixo F)
- Distribuicao proporcional ao aporte de cada cliente (TRUNCAR)
- Calculo e atualizacao do preco medio (media ponderada)
- Residuos mantidos na custodia master
- Publicacao de IR dedo-duro (0,005%) no Kafka

### Motor de Rebalanceamento
- Rebalanceamento por mudanca de composicao da cesta
- Venda de ativos removidos
- Compra de novos ativos com o valor obtido
- Rebalanceamento de ativos que mudaram percentual
- Rebalanceamento por desvio de proporcao (limiar de 5pp)
- Calculo de IR sobre vendas > R$ 20.000/mes (20% sobre lucro)
- Publicacao de IR sobre vendas no Kafka

### Regras Fiscais
- IR dedo-duro: 0,005% sobre cada operacao de compra
- IR sobre vendas: isento se total mensal <= R$ 20.000
- IR sobre vendas: 20% sobre lucro liquido se > R$ 20.000
- Preco medio NAO se altera em vendas (RN-043)

### Parser COTAHIST
- Leitura de arquivo TXT com layout posicional (245 caracteres)
- Filtro por tipo de registro (01 = detalhe)
- Filtro por mercado (010 = vista, 020 = fracionario)
- Filtro por BDI (02 = lote padrao, 96 = fracionario)
- Conversao de precos (2 casas decimais implicitas)
- Suporte a encoding ISO-8859-1

## Decisoes Tecnicas

1. **Clean Architecture**: Separacao clara entre Domain, Application, Infrastructure e API
2. **Repository Pattern**: Abstrai acesso a dados, facilita testes com mocks
3. **InMemory DB nos testes**: Testes rapidos sem dependencia de MySQL
4. **Docker Compose**: Toda a infraestrutura (MySQL, Kafka, API) sobe com um unico comando
5. **EnsureCreated no startup**: Auto-criacao do schema no primeiro deploy
6. **Kafka real**: Instancia real do Kafka via Docker, nao mock
7. **Tratamento de erros centralizado**: Exception handler global com codigos padronizados

## Estrutura do Arquivo COTAHIST

Os arquivos devem ser colocados na pasta `cotacoes/` com o formato `COTAHIST_DYYYYMMDD.TXT`. O sistema usa a cotacao de fechamento (campo PREULT, posicoes 109-121) do arquivo mais recente.
