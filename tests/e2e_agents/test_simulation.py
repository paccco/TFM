"""
Suite de pruebas pytest para la integración E2E con el A2A Gateway.
"""

from __future__ import annotations
import os
import pytest

try:
    from .run_simulation import (
        GATEWAY_URL,
        simulate_happy_path_negotiation,
        simulate_edge_case_invalid_params,
    )
except ImportError:
    from run_simulation import (
        GATEWAY_URL,
        simulate_happy_path_negotiation,
        simulate_edge_case_invalid_params,
    )


@pytest.fixture(scope="session")
def target_gateway_url() -> str:
    return os.getenv("GATEWAY_URL", GATEWAY_URL).rstrip("/")


def test_e2e_happy_path_negotiation(target_gateway_url: str):
    """Test Case 1: Negociación bilateral convergente entre Buyer y Seller."""
    success = simulate_happy_path_negotiation(base_url=target_gateway_url, max_turns=10)
    assert success is True, "La negociación determinista bilateral no convergió exitosamente"


def test_e2e_edge_cases_parameter_validation(target_gateway_url: str):
    """Test Case 2: Inyección de parámetros inválidos y verificación de -32602."""
    success = simulate_edge_case_invalid_params(base_url=target_gateway_url)
    assert success is True, "Las validaciones de parámetros en el Gateway no retornaron -32602"
