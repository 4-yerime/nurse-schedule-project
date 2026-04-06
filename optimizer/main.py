#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
NurseScheduler Pro - Python Genetic Algorithm Optimizer
Communicates with C# via stdin/stdout JSON protocol.

Protocol:
  INPUT:  stdin -> single JSON line
  OUTPUT: stdout -> LOG: <message>
                    PROGRESS: <gen>,<fitness>,<violations>
                    RESULT: <json>
"""

import sys
import json
import time
import traceback
from datetime import datetime, timedelta
from genetic_algorithm import GeneticAlgorithm


def log(msg: str):
    print(f"LOG:{msg}", flush=True)


def progress(gen: int, fitness: float, violations: int):
    print(f"PROGRESS:{gen},{fitness:.4f},{violations}", flush=True)


def result(data: dict):
    print(f"RESULT:{json.dumps(data, ensure_ascii=False)}", flush=True)


def main():
    try:
        # Force UTF-8 for stdin/stdout to handle emojis and Turkish characters correctly
        if hasattr(sys.stdin, 'reconfigure'):
            sys.stdin.reconfigure(encoding='utf-8')
        if hasattr(sys.stdout, 'reconfigure'):
            sys.stdout.reconfigure(encoding='utf-8')

        # Read JSON from stdin (entire stream until EOF)
        raw = sys.stdin.read().strip()
        if not raw:
            log("HATA: Boş girdi (stdin boş)")
            sys.exit(1)

        data = json.loads(raw)
        log(f"✅ Girdi alındı: {data.get('scheduleName', 'Bilinmeyen')}")
        log(f"📅 Tarih: {data['startDate']} → {data['endDate']}")
        log(f"👩‍⚕️ Hemşire sayısı: {len(data['nurses'])}")
        log(f"🏥 Alt birim sayısı: {len(data['subUnits'])}")
        log(f"📋 Kural sayısı: {len(data['rules'])}")
        log(f"⚙️  Algoritma modu: {data['algorithmMode']}")

        params = data.get('algorithmParams', {})
        log(f"🧬 Popülasyon: {params.get('populationSize',100)} | Nesil: {params.get('maxGenerations',300)}")

        # Run GA
        ga = GeneticAlgorithm(data, log_fn=log, progress_fn=progress)
        output = ga.run()

        result(output)

    except json.JSONDecodeError as e:
        log(f"JSON PARSE HATASI: {e}")
        sys.exit(1)
    except Exception as e:
        log(f"HATA: {traceback.format_exc()}")
        sys.exit(1)


if __name__ == "__main__":
    main()
