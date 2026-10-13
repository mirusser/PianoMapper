.DEFAULT_GOAL := run

.PHONY: run run-release publish-release check-docker

check-docker:
ifndef ConnectionStrings__PianoMapper
	@if ! docker info >/dev/null 2>&1; then \
		echo "Docker is not running. Start it with:" >&2; \
		echo "  sudo systemctl start docker" >&2; \
		exit 1; \
	fi
endif

run: check-docker
	./scripts/run-web.sh

run-release: check-docker
	./scripts/run-web.sh --aot

publish-release:
	./scripts/publish-aot.sh
