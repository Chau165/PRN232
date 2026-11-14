import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react"

export interface SearchableSelectOption {
  value: string
  label: string
}

type SearchableSingleSelectProps = {
  id: string
  label: string
  options: readonly SearchableSelectOption[]
  value: string
  onChange: (value: string) => void
  disabled?: boolean
  hint?: string
  placeholder?: string
}

function searchKey(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/gi, "d")
    .toLocaleLowerCase("vi")
    .trim()
}

export default function SearchableSingleSelect({
  id,
  label,
  options,
  value,
  onChange,
  disabled = false,
  hint,
  placeholder = "Tìm hoặc chọn...",
}: SearchableSingleSelectProps) {
  const generatedId = useId()
  const containerRef = useRef<HTMLDivElement>(null)
  const [isOpen, setIsOpen] = useState(false)
  const [searchText, setSearchText] = useState("")
  const [activeIndex, setActiveIndex] = useState(0)
  const selectedOption = options.find((option) => option.value === value)
  const search = searchKey(searchText)
  const filteredOptions = options.filter((option) =>
    searchKey(option.label).includes(search),
  )
  const listboxId = `${id}-${generatedId}-options`
  const hintId = hint ? `${id}-${generatedId}-hint` : undefined
  const activeOption = filteredOptions[activeIndex]

  useEffect(() => {
    if (!isOpen) return

    function closeOnOutsidePointer(event: PointerEvent) {
      if (
        event.target instanceof Node &&
        !containerRef.current?.contains(event.target)
      ) {
        setIsOpen(false)
        setSearchText("")
      }
    }

    document.addEventListener("pointerdown", closeOnOutsidePointer)
    return () => document.removeEventListener("pointerdown", closeOnOutsidePointer)
  }, [isOpen])

  function selectOption(option: SearchableSelectOption) {
    onChange(option.value)
    setIsOpen(false)
    setSearchText("")
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "Escape" && isOpen) {
      event.preventDefault()
      setIsOpen(false)
      setSearchText("")
      return
    }

    if (event.key === "Tab") {
      setIsOpen(false)
      setSearchText("")
      return
    }

    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault()
      if (!isOpen) {
        setIsOpen(true)
        setSearchText("")
        setActiveIndex(event.key === "ArrowDown" ? 0 : filteredOptions.length - 1)
        return
      }

      if (filteredOptions.length === 0) return
      setActiveIndex((currentIndex) => {
        const direction = event.key === "ArrowDown" ? 1 : -1
        return (currentIndex + direction + filteredOptions.length) % filteredOptions.length
      })
      return
    }

    if (event.key === "Enter" && isOpen) {
      event.preventDefault()
      if (activeOption) selectOption(activeOption)
    }
  }

  return (
    <div ref={containerRef} className="relative flex flex-col gap-1">
      <label
        htmlFor={id}
        className="text-sm font-semibold text-slate-700"
      >
        {label}
      </label>
      <input
        id={id}
        type="text"
        role="combobox"
        aria-autocomplete="list"
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-controls={isOpen ? listboxId : undefined}
        aria-activedescendant={isOpen && activeOption ? `${listboxId}-${activeOption.value}` : undefined}
        aria-describedby={hintId}
        autoComplete="off"
        disabled={disabled}
        placeholder={placeholder}
        value={isOpen ? searchText : selectedOption?.label ?? ""}
        onFocus={() => {
          if (disabled) return
          setIsOpen(true)
          setSearchText("")
          setActiveIndex(0)
        }}
        onChange={(event) => {
          setSearchText(event.target.value)
          setActiveIndex(0)
          setIsOpen(true)
        }}
        onKeyDown={handleKeyDown}
        onBlur={() => {
          setIsOpen(false)
          setSearchText("")
        }}
        className="min-h-11 rounded-lg border border-slate-200 px-3 py-2 font-normal outline-none focus:border-teal-700 focus:ring-2 focus:ring-teal-100 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500"
      />
      {hint && (
        <span id={hintId} className="text-xs font-normal text-slate-500">
          {hint}
        </span>
      )}
      {isOpen && (
        <ul
          id={listboxId}
          role="listbox"
          aria-label={`Danh sách ${label}`}
          className="absolute left-0 right-0 top-full z-[80] mt-1 max-h-56 overflow-y-auto rounded-lg border border-slate-200 bg-white py-1 shadow-lg"
        >
          {filteredOptions.length === 0 ? (
            <li className="px-3 py-2 text-sm font-normal text-slate-500">
              Không tìm thấy lựa chọn phù hợp.
            </li>
          ) : (
            filteredOptions.map((option, index) => (
              <li
                id={`${listboxId}-${option.value}`}
                key={option.value}
                role="option"
                aria-selected={option.value === value}
                onMouseDown={(event) => event.preventDefault()}
                onMouseEnter={() => setActiveIndex(index)}
                onClick={() => selectOption(option)}
                className={`cursor-pointer px-3 py-2 text-sm font-normal ${
                  index === activeIndex
                    ? "bg-teal-50 text-teal-900"
                    : "text-slate-700 hover:bg-slate-50"
                }`}
              >
                {option.label}
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  )
}
