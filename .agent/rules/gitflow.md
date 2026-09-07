# Reglas de Flujo de Trabajo Git (Gitflow) y Convenciones de Commit

Este documento establece el estándar obligatorio de ramificación, commits y versionado para cualquier desarrollo en el repositorio del **Orquestador Stateless de Negociación A2A**.

---

## 1. Topología de Ramas

El repositorio sigue un modelo basado en **Gitflow Adaptado** con dos ramas perennes principales y ramas de ciclo de vida efímero:

### Ramas Principales (Permanentes y Protegidas)
- **`main`**:
  - Representa el código en estado de producción o versión estable entregable.
  - Cada merge en `main` debe corresponder a un tag de versión semántica (ej. `v1.0.0`).
  - **Prohibido realizar commits directos**. Solo se integra mediante Pull Requests / merges formalizados desde `develop` o `hotfix/*`.
- **`develop`**:
  - Rama de integración continua y base para el desarrollo diario.
  - Aglutina las funcionalidades completadas y probadas.
  - **Prohibido realizar commits directos**. Todo cambio proviene de ramas `feature/*` validadas.

### Ramas de Soporte (Efímeras)
- **`feature/OP-<ID>-<slug>`**:
  - Creadas a partir de `develop` para implementar paquetes de trabajo específicos de OpenProject (Work Packages).
  - Nomenclatura obligatoria: `feature/OP-<ID>-<slug>`
    - `<ID>`: Identificador numérico del Work Package en OpenProject (ej. `OP-14`).
    - `<slug>`: Descripción breve en kebab-case (ej. `fsm-core-transitions`).
  - Ejemplo: `feature/OP-14-fsm-core-transitions`
- **`release/v<X.Y.Z>`**:
  - Creadas desde `develop` cuando se alcanza un hito de entrega para tareas de estabilización, documentación final y pruebas de regresión.
  - Se integra de vuelta en `main` y en `develop`.
- **`hotfix/OP-<ID>-<slug>`**:
  - Creadas directamente desde `main` para resolver incidencias críticas en producción.
  - Se integran en `main` (con nuevo patch tag) y en `develop`.

---

## 2. Convención de Mensajes de Commit (Conventional Commits)

Todos los mensajes de commit deben seguir estrictamente la especificación [Conventional Commits v1.0.0](https://www.conventionalcommits.org/), incorporando además la referencia al Work Package de OpenProject (`#<ID>`):

### Formato Estándar
```text
<tipo>(<ámbito>): <descripción imperativa en presente> (#<ID>)

[cuerpo explicativo opcional: detalles técnicos, justificación arquitectónica]

[pie opcional: BREAKING CHANGE, referencias adicionales]
```

### Tipos Permitidos
- **`feat`**: Nueva funcionalidad para el usuario o sistema (ej. endpoints, reglas FSM).
- **`fix`**: Corrección de un error o comportamiento anómalo.
- **`docs`**: Cambios exclusivos en la documentación técnica (`docs/` LaTeX, `README.md`, diagramas).
- **`refactor`**: Modificación de código que no añade funcionalidades ni repara errores.
- **`test`**: Creación o actualización de pruebas unitarias o de integración.
- **`chore`**: Tareas de mantenimiento, dependencias o configuración interna.
- **`perf`**: Mejoras específicas de rendimiento y optimización de latencia.

### Ámbitos (Scopes) Recomendados
- `gateway`: Capa de transporte A2A, Agent Card, mTLS y validación en cascada.
- `orchestrator`: Motor FSM, Deterministic Guard, MCDA y Fan-Out/Fan-In.
- `core`: DTOs, interfaces de dominio, contratos de estado.
- `infra`: Dual-track store (Redis Hot State o SQL Raw Transcript).
- `docs`: Documentación en LaTeX o especificaciones markdown.
- `agent`: Reglas del agente, workflows o scripts de OpenProject.

### Ejemplos Válidos
```bash
feat(gateway): implement cascade 4-phase validation pipeline (#12)
fix(orchestrator): prevent invalid transition from validation to ended without guard ok (#15)
docs(latex): complete formal fsm state mapping section (#18)
chore(infra): configure redis alpine aof persistence for turn locks (#20)
```

---

## 3. Protocolo de Integración y Pull Requests

1. **Antes de crear PR / Merge a `develop`:**
   - La suite de pruebas unitarias (`tests/unit/`) debe ejecutarse y pasar al 100%.
   - Los documentos LaTeX en `docs/` deben compilar sin advertencias críticas (`make -C docs`).
   - El código debe respetar la separación estricta de capas (.NET 9 Clean Architecture).
2. **Registro en OpenProject:**
   - Cada commit significativo debe reportarse mediante `python3 scripts/op_cli.py comment --id <ID> --text "Commit <HASH>: <descripción>"`.
3. **Estrategia de Merge:**
   - Se utiliza **Squash and Merge** o **Rebase & Merge** para mantener un historial lineal y limpio en `develop`, o merge commit con descripción explícita del Work Package integrado.
