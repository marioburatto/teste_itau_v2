#!/usr/bin/env python3
"""Generate a valid COTAHIST file with exactly 245 chars per line."""

import os

# Each line must be exactly 245 characters
# Layout:
# TIPREG(2) DATPRE(8) CODBDI(2) CODNEG(12) TPMERC(3) NOMRES(12) ESPECI(10) PRAZOT(3) MODREF(4)
# PREABE(13) PREMAX(13) PREMIN(13) PREMED(13) PREULT(13) PREOFC(13) PREOFV(13)
# TOTNEG(5) QUATOT(18) VOLTOT(18) PREEXE(13) INDOPC(1) DATVEN(8) FATCOT(7) PTOEXE(13) CODISI(12) DISMES(3)

def make_detail(date, bdi, ticker, tpmerc, nome, especi, preabe, premax, premin, premed, preult, preofc, preofv, totneg, quatot, voltot, codisi):
    line = "01"                           # TIPREG 2
    line += date                          # DATPRE 8
    line += bdi.ljust(2)                  # CODBDI 2
    line += ticker.ljust(12)              # CODNEG 12
    line += str(tpmerc).zfill(3)          # TPMERC 3
    line += nome.ljust(12)               # NOMRES 12
    line += especi.ljust(10)             # ESPECI 10
    line += "   "                         # PRAZOT 3
    line += "R$  "                        # MODREF 4
    line += str(preabe).zfill(13)        # PREABE 13
    line += str(premax).zfill(13)        # PREMAX 13
    line += str(premin).zfill(13)        # PREMIN 13
    line += str(premed).zfill(13)        # PREMED 13
    line += str(preult).zfill(13)        # PREULT 13
    line += str(preofc).zfill(13)        # PREOFC 13
    line += str(preofv).zfill(13)        # PREOFV 13
    line += str(totneg).zfill(5)         # TOTNEG 5
    line += str(quatot).zfill(18)        # QUATOT 18
    line += str(voltot).zfill(18)        # VOLTOT 18
    line += "0".zfill(13)                # PREEXE 13
    line += "0"                           # INDOPC 1
    line += "00000000"                    # DATVEN 8
    line += "0000001"                     # FATCOT 7
    line += "0".zfill(13)                # PTOEXE 13
    line += codisi.ljust(12)             # CODISI 12
    line += "180"                         # DISMES 3
    
    # Pad or trim to exactly 245
    line = line.ljust(245)[:245]
    return line

def make_header(date_str):
    line = "00COTAHIST." + date_str
    return line.ljust(245)[:245]

def make_trailer(count):
    line = "99COTAHIST.2026/02/25" + str(count).zfill(11)
    return line.ljust(245)[:245]

stocks = [
    # bdi, ticker, tpmerc, nome, especi, preabe, premax, premin, premed, preult, preofc, preofv, totneg, quatot, voltot, codisi
    ("02", "PETR4",    10, "PETROBRAS",  "PN      N1", 3520, 3650, 3480, 3560, 3500, 3490, 3510, 34561, 15000000, 537600000000, "BRPETRACNPR6"),
    ("02", "VALE3",    10, "VALE",       "ON      N1", 6150, 6300, 6100, 6200, 6200, 6190, 6210, 25432, 12000000, 744000000000, "BRVALEACNOR0"),
    ("02", "ITUB4",    10, "ITAUUNIBANCO","PN      N1", 2980, 3050, 2950, 3000, 3000, 2990, 3010, 45678, 20000000, 600000000000, "BRITUBACNPR1"),
    ("02", "BBDC4",    10, "BRADESCO",   "PN      N1", 1480, 1530, 1460, 1500, 1500, 1490, 1510, 38765, 25000000, 375000000000, "BRBBDCACNPR2"),
    ("02", "WEGE3",    10, "WEG",        "ON      N1", 3980, 4100, 3950, 4020, 4000, 3990, 4010, 19876, 8000000,  320000000000, "BRWEGEACNOR4"),
    ("02", "ABEV3",    10, "AMBEV",      "ON      N1", 1380, 1420, 1360, 1400, 1400, 1390, 1410, 32100, 30000000, 420000000000, "BRABEVACNOR2"),
    ("02", "RENT3",    10, "LOCALIZA",   "ON      N1", 4780, 4900, 4750, 4850, 4800, 4790, 4810, 15432, 6000000,  288000000000, "BRRENTACNOR1"),
    # Fractional
    ("96", "PETR4F",   20, "PETROBRAS",  "PN      N1", 3520, 3650, 3480, 3560, 3500, 3490, 3510, 4561,  1500000,  53760000000,  "BRPETRACNPR6"),
    ("96", "VALE3F",   20, "VALE",       "ON      N1", 6150, 6300, 6100, 6200, 6200, 6190, 6210, 2543,  1200000,  74400000000,  "BRVALEACNOR0"),
    ("96", "ITUB4F",   20, "ITAUUNIBANCO","PN      N1", 2980, 3050, 2950, 3000, 3000, 2990, 3010, 4567,  2000000,  60000000000,  "BRITUBACNPR1"),
    ("96", "BBDC4F",   20, "BRADESCO",   "PN      N1", 1480, 1530, 1460, 1500, 1500, 1490, 1510, 3876,  2500000,  37500000000,  "BRBBDCACNPR2"),
    ("96", "WEGE3F",   20, "WEG",        "ON      N1", 3980, 4100, 3950, 4020, 4000, 3990, 4010, 1987,  800000,   32000000000,  "BRWEGEACNOR4"),
    ("96", "ABEV3F",   20, "AMBEV",      "ON      N1", 1380, 1420, 1360, 1400, 1400, 1390, 1410, 3210,  3000000,  42000000000,  "BRABEVACNOR2"),
    ("96", "RENT3F",   20, "LOCALIZA",   "ON      N1", 4780, 4900, 4750, 4850, 4800, 4790, 4810, 1543,  600000,   28800000000,  "BRRENTACNOR1"),
]

date = "20260225"

lines = []
lines.append(make_header("2026/02/25"))

for s in stocks:
    line = make_detail(date, s[0], s[1], s[2], s[3], s[4], s[5], s[6], s[7], s[8], s[9], s[10], s[11], s[12], s[13], s[14], s[15])
    lines.append(line)

lines.append(make_trailer(len(stocks)))

# Write to cotacoes/ and test cotacoes
for path in [
    os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "cotacoes", "COTAHIST_D20260225.TXT"),
    os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "tests", "CompraProgramada.Tests", "cotacoes_test", "COTAHIST_D20260225.TXT"),
]:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="latin-1") as f:
        for line in lines:
            assert len(line) == 245, f"Line length {len(line)} != 245: {line[:50]}..."
            f.write(line + "\n")
    print(f"Written {path} ({len(lines)} lines, each 245 chars)")
