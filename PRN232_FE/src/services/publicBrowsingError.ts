export type PublicApiErrorKind = "validation" | "not-found" | "network" | "server" | "unexpected"

export class PublicApiError extends Error {
  readonly kind: PublicApiErrorKind
  readonly status?: number
  readonly validationErrors?: Record<string, string[]>

  constructor(
    kind: PublicApiErrorKind,
    message: string,
    options: {
      status?: number
      validationErrors?: Record<string, string[]>
    } = {},
  ) {
    super(message)
    this.name = "PublicApiError"
    this.kind = kind
    this.status = options.status
    this.validationErrors = options.validationErrors
  }
}
