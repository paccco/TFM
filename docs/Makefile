# Makefile para compilación de documentación formal LaTeX
# Orquestador Stateless de Negociación A2A

MAIN = main
PDF = $(MAIN).pdf
SRC = $(MAIN).tex sections/01_architecture.tex sections/02_fsm_states.tex sections/03_protocols.tex sections/04_persistence.tex

.PHONY: all pdf pdflatex latexmk clean

all: pdf

pdf: pdflatex

pdflatex: $(SRC)
	@echo "==> Compilando $(MAIN).tex (Pasada 1/2)..."
	pdflatex -interaction=nonstopmode -halt-on-error $(MAIN).tex > /dev/null
	@echo "==> Resolviendo referencias cruzadas y TOC (Pasada 2/2)..."
	pdflatex -interaction=nonstopmode -halt-on-error $(MAIN).tex > /dev/null
	@echo "==> Compilación finalizada exitosamente: $(PDF)"

latexmk: $(SRC)
	@if command -v latexmk >/dev/null 2>&1; then \
		latexmk -pdf -interaction=nonstopmode -halt-on-error $(MAIN).tex; \
	else \
		echo "latexmk no encontrado, utilizando pdflatex como alternativa..."; \
		$(MAKE) pdflatex; \
	fi

clean:
	@echo "==> Limpiando artefactos temporales de LaTeX..."
	rm -f *.aux *.log *.out *.toc *.synctex.gz *.fls *.fdb_latexmk
	rm -f sections/*.aux
	@echo "==> Limpieza completada."
