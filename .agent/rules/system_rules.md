# Directivas de Sistema y Arquitectura Técnica: Orquestador Stateless A2A

Este documento constituye la referencia arquitectónica y técnica vinculante para el diseño, desarrollo y gobernanza del **Orquestador Stateless de Negociación A2A**.

---

## 1. Visión y Principio Rector del Sistema

El sistema implementa un orquestador stateless, desacoplado y de baja latencia en ASP.NET Core / C# (.NET 9) para gobernar negociaciones comerciales automatizadas entre agentes de IA bajo el estándar público **A2A (Agent-to-Agent)** utilizando **JSON-RPC 2.0**.

> **PRINCIPIO FUNDAMENTAL:**
> **Determinismo Absoluto en el Núcleo.** La lógica de negocio, las reglas contractuales y las transiciones de estado son gobernadas exclusivamente por código procedural determinista en C# (FSM y Deterministic Guard).
> **Los LLMs tienen estrictamente prohibido alterar o inferir transiciones de estado.** Su rol se limita al razonamiento táctico, persuasión y formulación de lenguaje natural mediante esquemas estructurados (`facts_delta`).

---

## 2. Arquitectura del Sistema por Capas (5 Capas)

```text
┌─────────────────────────────────────────────────────────────┐
│  Capa 1: Clientes A2A (Agentes Externos / Comprador-Vendedor)│
└──────────────────────────────┬──────────────────────────────┘
                               │ JSON-RPC 2.0 (HTTPS / mTLS)
┌──────────────────────────────▼──────────────────────────────┐
│  Capa 2: Gateway A2A (ASP.NET Core / C#)                     │
│   - /.well-known/agent.json (Agent Card)                    │
│   - Seguridad Perimetral (mTLS, Rate Limiting, Cuotas)      │
│   - Pipeline de Validación en Cascada en 4 Fases            │
│   - Skill Dispatcher y Traducción de Payloads a DTOs        │
└──────────────────────────────┬──────────────────────────────┘
                               │ Enlace In-Process / Pod K8s
┌──────────────────────────────▼──────────────────────────────┐
│  Capa 3: Gobernanza y Orquestación FSM (C# / .NET 9)        │
│   - FSM Unitaria Determinista (100% Stateless)              │
│   - Deterministic Guard (Margen, Stock, Crédito, Plazos)    │
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

### Capa 1: Clientes A2A (Entorno Externo)
- Agentes de negociación autónomos que descubren dinámicamente el catálogo leyendo la Agent Card pública.
- Interactúan mediante JSON-RPC 2.0 estándar (`create_task`, `send_message`, `get_task`).

### Capa 2: Gateway A2A (ASP.NET Core / C#)
- Punto de entrada perimetral con mTLS, cuotas y rate limiting.
- Expone `/.well-known/agent.json`.
- **Pipeline de Validación en Cascada en 4 Fases**:
  1. *Fase 1 (Transporte):* Validación estructural, cabeceras HTTP y estructura JSON-RPC 2.0.
  2. *Fase 2 (Esquema A2A):* Conformidad de esquema estándar (`TaskRequest`, `MessagePart`, `DataPart`).
  3. *Fase 3 (Semántica Agent Card):* Existencia de la habilidad (`skill`) requerida y parámetros conformes.
  4. *Fase 4 (Coherencia de Ciclo de Vida):* Verificación de que la acción solicitada es legal para el estado actual de la negociación.

### Capa 3: Gobernanza y Orquestación FSM (C# / .NET 9)
- Alojada en el mismo Pod que el Gateway para eliminar sobrecostes de red y latencias de serialización.
- Arquitectura **100% stateless**: el motor no mantiene memoria de sesión en memoria RAM estática. En cada request rehidrata el estado desde Redis.
- **Deterministic Guard**: Validación de umbrales cuantitativos (margen mínimo de ganancia, disponibilidad de inventario en ERP, límites de riesgo de crédito). Si falla una regla de negocio, la propuesta se rechaza inmediatamente sin consultar al LLM.
- **Meta-Orquestador Fan-Out / Fan-In**: Coordina negociaciones multilaterales concurrentes (1 comprador vs $N$ proveedores) mediante `Task.WhenAll` y control de tiempos con `CancellationTokenSource`.
- **Evaluador MCDA**: Resolución de subastas multilaterales aplicando una función de utilidad ponderada:
  $$\text{Score} = w_1 \cdot P_{\text{norm}} + w_2 \cdot T_{\text{norm}} + w_3 \cdot D_{\text{norm}} + w_4 \cdot R_{\text{norm}}$$

### Capa 4: Capa Cognitiva (LLM SDK)
- Emite propuestas comerciales mediante `facts_delta` tipado (JSON Schema).
- **Bucle de Autocorrección**: Si el Gateway u Orquestador retornan HTTP 422 (error de validación de formato o transición inválida), el agente reinyecta el error de diagnóstico en su prompt, con un límite máximo de $N=2$ o $3$ reintentos. Si se supera el límite, la sesión transita a `frozen` (failed) para mediación humana.

### Capa 5: Persistencia Dual-Track State Store
- **Track 1 (Raw Transcript - SQL Relacional):** Almacenamiento cronológico, inmutable y append-only de cada turno (emisor, mensaje literal, timestamp, firma) para auditoría legal forense.
- **Track 2 (Hot State - Redis):** Fact Sheet consolidado y acumulativo (precios, volúmenes, Incoterms, cláusulas acordadas) con latencia $< 2\text{ ms}$ y control estricto de concurrencia y alternancia de turnos.

---

## 3. Mapeo Bidireccional de Estados FSM

La siguiente tabla define la correspondencia unívoca entre los estados públicos del estándar A2A y los estados internos de la FSM de gobernanza en C#:

| Estado A2A (Público) | Estado FSM (Interno C#) | Semántica Operativa y Reglas de Flujo |
| :--- | :--- | :--- |
| `working` | `on_board` | Fase activa de diálogo comercial, intercambio de ofertas y contraofertas. |
| `input_required` | `board_format_err` | Error sintáctico o discrepancia de formato en payload de negociación. Activa bucle de autocorrección LLM. |
| `evaluating` | `validation` | Fact Sheet sometido a revisión en Deterministic Guard (reglas de margen, stock, crédito). Si no cumple, vuelve a `on_board`. |
| `input_required` | `val_format_err` | Discrepancia de esquema de datos entre el Fact Sheet y el backend de negocio durante la validación. |
| `completed` | `ended` | Acuerdo comercial plenamente validado por ambas partes, cerrado y firmado contractualmente. |
| `failed` | `frozen` | Sesión congelada o fallida por superación de reintentos ($N > 3$) o anomalía severa. Requiere intervención humana. |

---

## 4. Diagramas de Secuencia y Trazas Formales

La especificación formal y el código fuente ejecutable de los diagramas de secuencia del sistema residen en el directorio `mermaids/` del repositorio:
- `mermaids/Diagrama de secuencias handshake.txt`: Protocolo de apretón de manos, autenticación de tenants, validación de whitelist y selección de Agent Card.
- `mermaids/Diagrma de secuencias negociaion.txt`: Ciclo de negociación, gestión de turnos, autocorrección ante errores y transición a validación/cierre.

Todo agente debe consultar directamente dichos archivos al diseñar o implementar flujos de comunicación.
