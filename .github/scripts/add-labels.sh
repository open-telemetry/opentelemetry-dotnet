#!/usr/bin/env bash
#
# Copyright The OpenTelemetry Authors
# SPDX-License-Identifier: Apache-2.0
#

set -euo pipefail

if [[ -z "${ISSUE:-}" || -z "${COMMENT:-}" || -z "${SENDER:-}" ]]; then
    echo "At least one of ISSUE, COMMENT, or SENDER has not been set, please ensure each is set."
    exit 0
fi

CUR_DIRECTORY=$(dirname "$0")

echo "${COMMENT}"

gh issue edit "${ISSUE}" --add-label "help wanted"
