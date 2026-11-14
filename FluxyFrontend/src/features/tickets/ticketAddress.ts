/**
 * Where a just-created ticket's conversation lives, built from the API's own `Location`.
 *
 * The create endpoint answers `201 ticket_created` with `Location: /auth/tickets/{id}` - the
 * **API's** path, which is not a route this router has. Following it directly lands the visitor
 * on the 404 page; a runtime check of the create flow found exactly that. The id is the part
 * that travels, and the address is rebuilt under the caller's own list path, so a renamed area
 * or route reaches the link as well.
 *
 * Written as a function rather than inline in the page because the rule has three cases worth
 * pinning (a normal header, a header with a query string, and no usable header at all) and a
 * page cannot be unit-tested here. `null` means "stay on the list": the caller re-reads it and
 * the new ticket is first by the default order.
 */
export const createdTicketPath = (
  listPath: string | null,
  location: string | null,
): string | null => {
  if (!listPath || !location) return null

  // The query string is not part of the identifier, and a trailing slash should not make the
  // last segment empty: both are shapes a server may legitimately answer with.
  const id = location.split('?')[0]?.split('#')[0]?.split('/').filter(Boolean).pop()
  if (!id) return null

  return `${listPath}/${id}`
}
