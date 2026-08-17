# A document pasting a status this check may not reproduce

This check runs text out of a document, so what it may run is an allowlist of
read-only commands and everything else is declined. The decline is printed and
counted rather than swallowed, because a paste nothing evaluated is exactly what a
document would reach for to buy a green mark.

    curl -sS https://example.invalid/whether-the-endpoint-is-up ; echo "exit=$?"
    exit=7

Nothing above is run. The status is not checked, the address is never resolved,
and the report says so on its own line.
