#!/bin/bash
set -e
BASE="http://localhost:8080"

echo "=== Test 0: Create Basket ==="
curl -s -X POST "$BASE/api/admin/cesta" \
  -H 'Content-Type: application/json' \
  -d '{"nome":"Top Five - Fevereiro 2026","itens":[{"ticker":"PETR4","percentual":30},{"ticker":"VALE3","percentual":25},{"ticker":"ITUB4","percentual":20},{"ticker":"BBDC4","percentual":15},{"ticker":"WEGE3","percentual":10}]}'
echo ""

echo "=== Test 1: Add Client A ==="
curl -s -X POST "$BASE/api/clientes/adesao" \
  -H 'Content-Type: application/json' \
  -d '{"nome":"Joao da Silva","cpf":"12345678901","email":"joao@email.com","valorMensal":3000}'
echo ""

echo "=== Test 2: Add Client B ==="
curl -s -X POST "$BASE/api/clientes/adesao" \
  -H 'Content-Type: application/json' \
  -d '{"nome":"Maria Souza","cpf":"98765432109","email":"maria@email.com","valorMensal":6000}'
echo ""

echo "=== Test 3: Add Client C ==="
curl -s -X POST "$BASE/api/clientes/adesao" \
  -H 'Content-Type: application/json' \
  -d '{"nome":"Pedro Santos","cpf":"55566677788","email":"pedro@email.com","valorMensal":1500}'
echo ""

echo "=== Test 4: Duplicate CPF (should fail) ==="
curl -s -X POST "$BASE/api/clientes/adesao" \
  -H 'Content-Type: application/json' \
  -d '{"nome":"Joao Clone","cpf":"12345678901","email":"clone@email.com","valorMensal":1000}'
echo ""

echo "=== Test 5: Get Current Basket ==="
curl -s "$BASE/api/admin/cesta/atual"
echo ""

echo "=== Test 6: Execute Purchase (2026-02-25) ==="
curl -s -X POST "$BASE/api/motor/executar-compra" \
  -H 'Content-Type: application/json' \
  -d '{"dataReferencia":"2026-02-25"}'
echo ""

echo "=== Test 7: Consult Client 1 Portfolio ==="
curl -s "$BASE/api/clientes/1/carteira"
echo ""

echo "=== Test 8: Consult Client 1 Rentabilidade ==="
curl -s "$BASE/api/clientes/1/rentabilidade"
echo ""

echo "=== Test 9: Consult Master Custody ==="
curl -s "$BASE/api/admin/conta-master/custodia"
echo ""

echo "=== Test 10: Alter Monthly Value ==="
curl -s -X PUT "$BASE/api/clientes/1/valor-mensal" \
  -H 'Content-Type: application/json' \
  -d '{"novoValorMensal":6000}'
echo ""

echo "=== Test 11: Client Exit ==="
curl -s -X POST "$BASE/api/clientes/3/saida"
echo ""

echo "=== Test 12: Basket History ==="
curl -s "$BASE/api/admin/cesta/historico"
echo ""

echo "=== Test 13: Duplicate Purchase (should fail) ==="
curl -s -X POST "$BASE/api/motor/executar-compra" \
  -H 'Content-Type: application/json' \
  -d '{"dataReferencia":"2026-02-25"}'
echo ""

echo "=== Test 14: Change Basket (triggers rebalancing) ==="
curl -s -X POST "$BASE/api/admin/cesta" \
  -H 'Content-Type: application/json' \
  -d '{"nome":"Top Five - Marco 2026","itens":[{"ticker":"PETR4","percentual":25},{"ticker":"VALE3","percentual":20},{"ticker":"ITUB4","percentual":20},{"ticker":"ABEV3","percentual":20},{"ticker":"RENT3","percentual":15}]}'
echo ""

echo "=== ALL API TESTS DONE ==="
