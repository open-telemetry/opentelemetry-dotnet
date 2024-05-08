#!/usr/bin/env bash
#
# Copyright The OpenTelemetry Authors
# SPDX-License-Identifier: Apache-2.0
#

set -euo pipefail

if [[ -z "${ISSUE_NUMBER:-}" || -z "${ISSUE_BODY:-}" || -z "${SENDER:-}" ]]; then
    echo "At least one of ISSUE_NUMBER, ISSUE_BODY, or SENDER has not been set, please ensure each is set."
    exit 0
fi

CUR_DIRECTORY=$(dirname "$0")

echo "ISSUE_NUMBER: ${ISSUE_NUMBER}"
echo "ISSUE_BODY: ${ISSUE_BODY}"
echo "SENDER: ${SENDER}"

gh issue edit "${ISSUE_NUMBER}" --add-label "help wanted"
