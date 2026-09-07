# Orquestador Stateless de Negociación A2A

[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20Hexagonal-blue.svg)]()
[![Protocol](https://img.shields.io/badge/Protocol-JSON--RPC%202.0%20%2F%20A2A-green.svg)]()
[![Persistence](https://img.shields.io/badge/Persistence-Dual--Track%20(Redis%20%2B%20SQL)-red.svg)]()
[![Documentation](https://img.shields.io/badge/Docs-LaTeX%20(pdflatex)-yellow.svg)](docs/main.pdf)

Orquestador *stateless*, desacoplado y de baja latencia desarrollado sobre **ASP.NET Core y C# (.NET 9)** para arbitrar y gobernar negociaciones comerciales entre agentes autónomos bajo el estándar público **A2A (Agent-to-Agent)** utilizando **JSON-RPC 2.0**.

El núcleo del sistema garantiza el **determinismo absoluto del flujo** mediante una Máquina de Estados Finitos (FSM) procedural y un motor de reglas duras (*Deterministic Guard*), impidiendo de manera categórica que las alucinaciones o respuestas probabilísticas de los Modelos de Lenguaje (LLMs) alteren las transiciones de estado o las condiciones contractuales.

---

## 1. Arquitectura del Sistema (5 Capas)

```text
┌─────────────────────────────────────────────────────────────┐
│  Capa 1: Clientes A2A (Agentes Externos / Comprador-Vendedor)│
└──────────────────────────────┬──────────────────────────────┘
                               │ JSON-RPC 2.0 (HTTPS / mTLS)
┌──────────────────────────────▼──────────────────────────────┐
│  Capa 2: Gateway A2A (ASP.NET Core / C#)                     │
│   - /.well-known/agent.json (Agent Card)                    │
│   - Seguridad Perimetral (mTLS, Rate Limiting Token Bucket) │
│   - Pipeline de Validación en Cascada en 4 Fases            │
│   - Skill Dispatcher y Traducción de Payloads a DTOs        │
└──────────────────────────────┬──────────────────────────────┘
                               │ Enlace In-Process (Pod K8s)
┌──────────────────────────────▼──────────────────────────────┐
│  Capa 3: Gobernanza y Orquestación FSM (C# / .NET 9)        │
│   - FSM Unitaria Determinista (100% Stateless)              │
│   - Deterministic Guard (Margen, Stock ERP, Crédito, Plazos)│
│   - Meta-Orquestador Fan-Out / Fan-In (Task.WhenAll, CTS)   │
│   - Evaluador Multicriterio MCDA (Score = Σ wi · xi)        │
└──────────────────────────────┬──────────────────────────────┘
                               │
            ┌──────────────────┴──────────────────┐
            ▼                                     ▼
┌──────────────────────────────┐    ┌─────────────────────────────────┐
│ Capa 4: Capa Cognitiva       │    │ Capa 5: Dual-Track State Store  │
│  - LLM SDK + Structured Out  │    │  - Track 1: SQL Raw Transcript  │
│  - Facts Delta               │    │    (Append-only, auditoría)     │
│  - Self-Correction (N ≤ 3)   │    │  - Track 2: Redis Hot State     │
│  - HTTP 422 Feedback Loop    │    │    (Fact Sheet, Turn Lock <2ms) │
└──────────────────────────────┘    └─────────────────────────────────┘
```

### Principios Fundamentales
1. **Separación Estricta de Responsabilidades:** El LLM razona y persuade; C# gobierna, audita y valida.
2. **Arquitectura 100% Stateless:** Cada solicitud rehidrata el estado fáctico desde Redis; no existe memoria volátil en proceso.
3. **Pipeline en Cascada en 4 Fases:** Transporte $\rightarrow$ Esquema A2A $\rightarrow$ Semántica Agent Card $\rightarrow$ Coherencia de Ciclo de Vida y Turnos.
4. **Persistencia Dual-Track:** Desacopla la pista de ultra-baja latencia en memoria (Redis) de la bitácora legal inmutable y cronológica (SQL).

---

## 2. Mapeo Bidireccional de Estados FSM

| Estado A2A (Público) | Estado FSM (Interno C#) | Tipo | Semántica Operativa |
| :--- | :--- | :--- | :--- |
| `working` | `on_board` | Transitorio | Diálogo activo, intercambio de propuestas y contraofertas. |
| `input_required` | `board_format_err` | Recuperable | Error de sintaxis en payload de negociación. Activa autocorrección. |
| `evaluating` | `validation` | Auditoría | Verificación del acuerdo contra reglas duras en Deterministic Guard. |
| `input_required` | `val_format_err` | Recuperable | Discrepancia de esquema de validación entre Fact Sheet y backend. |
| `completed` | `ended` | Terminal | Acuerdo validado con éxito por ambas partes y firmado formalmente. |
| `failed` | `frozen` | Terminal / Pausa | Sesión congelada por superación de reintentos ($N > 3$) o rechazo definitivo. |

---

## 3. Estructura del Repositorio

```text
.
├── .agent/
│   ├── rules/
│   │   ├── gitflow.md          # Flujo de ramas, Conventional Commits (#X) y PRs
│   │   ├── openproject.md      # Protocolo de trazabilidad e integración OpenProject CLI
│   │   └── system_rules.md     # Especificación arquitectónica, FSM y directivas del sistema
│   ├── workflows/              # Definiciones de flujos automatizados de agentes
│   └── logs/                   # Registro de trazas de ejecución de agentes
├── .antigravity/
│   └── config.yaml             # Configuración base del runtime Antigravity
├── scripts/
│   ├── op_cli.py               # CLI de integración con OpenProject API v3
│   └── dev/                    # Directorio reservado para entorno local (.gitkeep)
│       └── .gitkeep
├── docs/                       # Documentación técnica y formal en LaTeX
│   ├── main.tex                # Documento raíz con preámbulo académico formal
│   ├── sections/
│   │   ├── 01_architecture.tex # Detalle de las 5 capas y dual-track store
│   │   ├── 02_fsm_states.tex   # Mapeo de estados, validaciones y autocorrección
│   │   └── 03_protocols.tex    # A2A Gateway, JSON-RPC 2.0 y Agent Card
│   ├── figures/                # Diagramas e imágenes de la memoria técnica (.gitkeep)
│   └── Makefile                # Reglas pdflatex / latexmk y target clean
├── mermaids/                   # Diagramas de secuencia y flujo de negociación existentes
│   ├── Diagrama de secuencias handshake.txt
│   └── Diagrma de secuencias negociaion.txt
├── src/                        # Módulos de código .NET 9 (scaffolding base con .gitkeep)
│   ├── A2A.Gateway/            # Endpoints JSON-RPC 2.0, Agent Card, validación en 4 fases
│   ├── A2A.Orchestrator/       # FSM determinista, Deterministic Guard, MCDA, Fan-Out/Fan-In
│   ├── A2A.Core/               # DTOs tipados, interfaces de dominio, contratos de estado
│   └── A2A.Infrastructure/     # Persistencia Dual-Track (Redis Hot State + SQL Transcript)
├── tests/                      # Suites de pruebas automatizadas (scaffolding base con .gitkeep)
│   ├── unit/                   # Pruebas unitarias de FSM, Guard y evaluación MCDA
│   └── integration/            # Pruebas de integración de Gateway y persistencia
├── .gitignore                  # Exclusiones exhaustivas (.NET, LaTeX, Python, Docker, OS)
└── README.md                   # Documentación principal del repositorio
```

---

## 4. Guía de Operación y Desarrollo

### Requisitos del Sistema
- **Python 3.10+** (para `scripts/op_cli.py`)
- **TeX Live / pdflatex** (para compilar `docs/main.tex`)
- **.NET 9 SDK** (para el desarrollo posterior de los módulos en `src/`)

### Infraestructura Local
La infraestructura local (Redis para Hot State, base de datos relacional para Raw Transcript, contenedores y variables de entorno) se abordará y versionará en sus tareas técnicas específicas de OpenProject bajo sus ramas `feature/` correspondientes.

### Compilar la Documentación Técnica Formal
La memoria formal del proyecto está redactada en LaTeX. Puedes compilar el documento ejecutando:
```bash
make -C docs
```
Esto generará `docs/main.pdf` resolviendo referencias y tabla de contenidos. Para limpiar archivos auxiliares:
```bash
make -C docs clean
```

### Paso 4: Trazabilidad con OpenProject CLI
Para interactuar con los paquetes de trabajo de OpenProject:
```bash
# Autodescubrimiento de IDs de la instancia
python3 scripts/op_cli.py discover

# Actualizar estado al iniciar una tarea
python3 scripts/op_cli.py update --id <ID> --status in_progress --comment "Iniciando rama feature/OP-<ID>-<slug>"

# Comentar un commit en la tarea
python3 scripts/op_cli.py comment --id <ID> --text "Commit <HASH>: <descripción>"
```

---

## 5. Licencia y Gobernanza
Proyecto desarrollado como Trabajo de Fin de Máster (TFM) bajo arquitectura de sistemas multiagente deterministas y gobernanza de protocolos A2A.