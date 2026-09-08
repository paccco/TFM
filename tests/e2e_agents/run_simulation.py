#!/usr/bin/env python3
"""
Arnés de pruebas de integración End-to-End (E2E) con agentes simulados deterministas.
Verifica la conformidad del A2A Gateway (.NET 9 / ASP.NET Core) bajo el estándar JSON-RPC 2.0.

Casos de prueba:
1. Happy Path: Negociación bilateral convergente entre BuyerAgent y SellerAgent
   con alternancia monótona de turnos y retorno status="acknowledged".
2. Edge Cases: Inyección de parámetros inválidos (turn_index negativo, omisión de message,
   y discrepancia de identidad perimetral) retornando error formal -32602 sin HTTP 500.
"""

from __future__ import annotations
import json
import os
import sys
import uuid
import httpx

# Asegurar importación de módulos locales cuando se ejecuta directamente
current_dir = os.path.dirname(os.path.abspath(__file__))
if current_dir not in sys.path:
    sys.path.insert(0, current_dir)

try:
    from .schemas import (
        JsonRpcRequest,
        JsonRpcResponse,
        NegotiationAction,
        NegotiationDispatchResult,
        NegotiationMessagePayload,
        SendMessageParams,
    )
    from .agents import BuyerAgent, SellerAgent
except ImportError:
    from schemas import (
        JsonRpcRequest,
        JsonRpcResponse,
        NegotiationAction,
        NegotiationDispatchResult,
        NegotiationMessagePayload,
        SendMessageParams,
    )
    from agents import BuyerAgent, SellerAgent

GATEWAY_URL = os.getenv("GATEWAY_URL", "http://127.0.0.1:5000").rstrip("/")
RPC_ENDPOINT = f"{GATEWAY_URL}/rpc"
A2A_ENDPOINT = f"{GATEWAY_URL}/a2a"


def log_step(title: str, ok: bool = True, details: str = "") -> None:
    prefix = "[PASS]" if ok else "[FAIL]"
    msg = f"{prefix} {title}"
    if details:
        msg += f" -> {details}"
    print(msg)


def simulate_happy_path_negotiation(base_url: str = GATEWAY_URL, max_turns: int = 10) -> bool:
    """
    Test Case 1: Negociación determinista completa entre BuyerAgent y SellerAgent.
    Alterna turnos monótonos (0, 1, 2, ...) sobre el endpoint /rpc o /a2a.
    Verifica que cada respuesta del Gateway retorne status 'acknowledged' y converja en ACCEPT.
    """
    print("\n" + "=" * 70)
    print("TEST CASE 1: Happy Path - Negociación Determinista Bilateral (A2A Gateway)")
    print("=" * 70)

    endpoint = f"{base_url}/rpc"
    negotiation_id = f"neg-{uuid.uuid4().hex[:12]}"
    print(f"[*] Negotiation ID: {negotiation_id}")
    print(f"[*] Gateway Endpoint: {endpoint}")

    buyer = BuyerAgent(
        agent_id="urn:agent:buyer",
        initial_price=75.0,
        reservation_price=95.0,
        step=5.0,
    )
    seller = SellerAgent(
        agent_id="urn:agent:seller",
        initial_price=105.0,
        reservation_price=85.0,
        step=5.0,
    )

    buyer.reset()
    seller.reset()

    active_agent = buyer
    counterparty = seller
    last_payload: NegotiationMessagePayload | None = None
    converged = False

    with httpx.Client(timeout=10.0) as client:
        for turn in range(max_turns):
            # 1. Generar la acción determinista del agente activo
            action_payload = active_agent.respond(last_payload)
            print(
                f"[Turn {turn}] {active_agent.agent_id} -> Action: {action_payload.action.value}, "
                f"Price: {action_payload.proposal.unit_price if action_payload.proposal else 'N/A'} EUR"
            )

            # 2. Construir la petición JSON-RPC 2.0
            req = active_agent.build_send_message_request(
                negotiation_id=negotiation_id,
                turn_index=turn,
                payload=action_payload,
            )

            # 3. Despachar petición HTTP POST con cabecera X-Agent-ID correspondiente
            req_body = req.model_dump(by_alias=True)
            response = client.post(
                endpoint,
                headers=active_agent.http_headers,
                json=req_body,
            )

            # Validar HTTP 200 (Gateway perimetral siempre retorna 200 en JSON-RPC)
            if response.status_code != 200:
                log_step(f"Turn {turn} HTTP Status", False, f"Expected 200, got {response.status_code}: {response.text}")
                return False

            resp_json = response.json()
            rpc_resp = JsonRpcResponse[NegotiationDispatchResult].model_validate(resp_json)

            # Validar ausencia de errores JSON-RPC
            if rpc_resp.is_error or rpc_resp.result is None:
                log_step(f"Turn {turn} JSON-RPC dispatch", False, f"Error received: {rpc_resp.error}")
                return False

            result = rpc_resp.result
            if result.status != "acknowledged":
                log_step(f"Turn {turn} Status check", False, f"Expected 'acknowledged', got '{result.status}'")
                return False

            if result.turn_index != turn:
                log_step(f"Turn {turn} TurnIndex check", False, f"Expected {turn}, got {result.turn_index}")
                return False

            log_step(f"Turn {turn} Acknowledged by Gateway", True, f"negotiationId={result.negotiation_id}, turnIndex={result.turn_index}")

            # 4. Comprobar si se ha alcanzado el acuerdo
            if action_payload.action == NegotiationAction.ACCEPT:
                converged = True
                agreed_price = action_payload.proposal.unit_price if action_payload.proposal else None
                print(f"[+] CONVERGENCIA ALCANZADA en el turno {turn} con precio acordado: {agreed_price} EUR.")
                break

            # 5. Alternar turnos
            last_payload = action_payload
            active_agent, counterparty = counterparty, active_agent

    if not converged:
        log_step("Negotiation Convergence", False, f"Did not converge within {max_turns} turns.")
        return False

    log_step("Test Case 1: Happy Path", True, f"Bilateral negotiation converged successfully in {turn + 1} turns.")
    return True


def simulate_edge_case_invalid_params(base_url: str = GATEWAY_URL) -> bool:
    """
    Test Case 2: Edge Cases - Validación de error -32602 (Invalid params) en Gateway.
    Verifica que el pipeline en cascada (Fases 2 y 4) capture violaciones estructurales
    y responda con código -32602 y HTTP 200, sin provocar HTTP 500.
    """
    print("\n" + "=" * 70)
    print("TEST CASE 2: Edge Cases - Errores de Parámetros (-32602) en Gateway")
    print("=" * 70)

    endpoint = f"{base_url}/rpc"
    all_passed = True

    with httpx.Client(timeout=10.0) as client:
        # -------------------------------------------------------------
        # Sub-caso 2a: turn_index negativo (-1)
        # -------------------------------------------------------------
        print("\n[*] Sub-caso 2a: Enviar turn_index negativo (-1)")
        payload_2a = {
            "jsonrpc": "2.0",
            "method": "send_message",
            "params": {
                "negotiation_id": "neg-test-negative-turn",
                "turn_index": -1,
                "sender_id": "urn:agent:buyer",
                "message": {"action": "OFFER", "text": "invalid turn"},
            },
            "id": "test-sub-2a",
        }
        resp = client.post(
            endpoint,
            headers={"X-Agent-ID": "urn:agent:buyer", "Content-Type": "application/json"},
            json=payload_2a,
        )

        if resp.status_code != 200:
            log_step("Sub-caso 2a HTTP Status", False, f"Expected 200, got {resp.status_code}")
            all_passed = False
        else:
            data = resp.json()
            error = data.get("error", {})
            if error.get("code") == -32602:
                log_step("Sub-caso 2a Error Code -32602", True, f"Detail: {error.get('message')}")
            else:
                log_step("Sub-caso 2a Error Code", False, f"Expected -32602, got {error.get('code')}")
                all_passed = False

        # -------------------------------------------------------------
        # Sub-caso 2b: Ausencia del campo obligatorio 'message'
        # -------------------------------------------------------------
        print("\n[*] Sub-caso 2b: Omitir campo obligatorio 'message'")
        payload_2b = {
            "jsonrpc": "2.0",
            "method": "send_message",
            "params": {
                "negotiation_id": "neg-test-missing-message",
                "turn_index": 0,
                "sender_id": "urn:agent:buyer",
            },
            "id": "test-sub-2b",
        }
        resp = client.post(
            endpoint,
            headers={"X-Agent-ID": "urn:agent:buyer", "Content-Type": "application/json"},
            json=payload_2b,
        )

        if resp.status_code != 200:
            log_step("Sub-caso 2b HTTP Status", False, f"Expected 200, got {resp.status_code}")
            all_passed = False
        else:
            data = resp.json()
            error = data.get("error", {})
            if error.get("code") == -32602:
                log_step("Sub-caso 2b Error Code -32602", True, f"Detail: {error.get('message')}")
            else:
                log_step("Sub-caso 2b Error Code", False, f"Expected -32602, got {error.get('code')}")
                all_passed = False

        # -------------------------------------------------------------
        # Sub-caso 2c: Discrepancia entre cabecera X-Agent-ID y sender_id
        # -------------------------------------------------------------
        print("\n[*] Sub-caso 2c: Discrepancia X-Agent-ID vs sender_id")
        payload_2c = {
            "jsonrpc": "2.0",
            "method": "send_message",
            "params": {
                "negotiation_id": "neg-test-identity-mismatch",
                "turn_index": 0,
                "sender_id": "urn:agent:seller",  # Discrepancia intencional
                "message": {"action": "OFFER", "text": "spoofed sender"},
            },
            "id": "test-sub-2c",
        }
        resp = client.post(
            endpoint,
            headers={"X-Agent-ID": "urn:agent:buyer", "Content-Type": "application/json"},  # Autenticado como buyer
            json=payload_2c,
        )

        if resp.status_code != 200:
            log_step("Sub-caso 2c HTTP Status", False, f"Expected 200, got {resp.status_code}")
            all_passed = False
        else:
            data = resp.json()
            error = data.get("error", {})
            if error.get("code") == -32602 and "Identity mismatch" in error.get("message", ""):
                log_step("Sub-caso 2c Identity Mismatch Check (-32602)", True, f"Detail: {error.get('message')}")
            else:
                log_step("Sub-caso 2c Identity Mismatch Check", False, f"Expected -32602 with identity mismatch, got {error}")
                all_passed = False

    log_step("Test Case 2: Edge Cases", all_passed, "All perimeter validation checks returned -32602 without 500.")
    return all_passed


def main() -> int:
    print("=" * 70)
    print("A2A Gateway - Runner de Pruebas de Integración E2E con Agentes")
    print(f"Target Gateway URL: {GATEWAY_URL}")
    print("=" * 70)

    # 1. Verificar conectividad con Gateway
    try:
        with httpx.Client(timeout=5.0) as client:
            health = client.get(f"{GATEWAY_URL}/.well-known/agent.json")
            if health.status_code != 200:
                print(f"[FATAL] Healthcheck failed: {health.status_code} on {GATEWAY_URL}/.well-known/agent.json")
                return 1
            print(f"[+] Gateway conectado exitosamente (Agent Card respondida con HTTP 200).")
    except Exception as ex:
        print(f"[FATAL] No se pudo conectar con el Gateway en {GATEWAY_URL}: {ex}")
        return 1

    # 2. Ejecutar Test Case 1: Happy Path
    happy_ok = simulate_happy_path_negotiation(GATEWAY_URL)

    # 3. Ejecutar Test Case 2: Edge Cases
    edge_ok = simulate_edge_case_invalid_params(GATEWAY_URL)

    print("\n" + "=" * 70)
    print("RESUMEN DE PRUEBAS E2E:")
    print(f"  Happy Path (Negociación Determinista): {'PASADO' if happy_ok else 'FALLADO'}")
    print(f"  Edge Cases (-32602 Invalid Params):   {'PASADO' if edge_ok else 'FALLADO'}")
    print("=" * 70)

    if happy_ok and edge_ok:
        print("\n>>> TODOS LOS TESTS E2E HAN SIDO SUPERADOS CON ÉXITO <<<\n")
        return 0
    else:
        print("\n>>> AL MENOS UN TEST E2E HA FALLADO <<<\n")
        return 1


if __name__ == "__main__":
    sys.exit(main())
