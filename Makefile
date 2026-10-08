.DEFAULT_GOAL := run

.PHONY: run
run:
ifndef ConnectionStrings__PianoMapper
	@if ! docker info >/dev/null 2>&1; then \
		echo "Docker is not running. Start it with:" >&2; \
		echo "  sudo systemctl start docker" >&2; \
		exit 1; \
	fi
endif
	./scripts/run-web.sh
