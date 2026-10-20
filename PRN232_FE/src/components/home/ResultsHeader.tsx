type ResultsHeaderProps = {
  totalCount: number

  sortBy: string

  onSortChange: (sortBy: string) => void
}

export default function ResultsHeader({
  totalCount,
  sortBy,
  onSortChange,
}: ResultsHeaderProps) {
  return (
    <div className="mb-5 flex items-end justify-between">
      <p className="text-[14px] font-semibold text-slate-600">
        {totalCount} kết quả
      </p>
      <label className="flex items-center gap-2 rounded-lg bg-slate-50 px-3.5 py-2.5 text-[13px] font-medium text-slate-600">
        Sắp xếp:
        <select
          aria-label="Sắp xếp"
          value={sortBy}
          onChange={(event) => onSortChange(event.target.value)}
          className="bg-transparent font-semibold text-slate-800 outline-none"
        >
          <option value="propertyId">Mặc định</option>
          <option value="price_asc">Giá thấp đến cao</option>
          <option value="price_desc">Giá cao đến thấp</option>
        </select>
      </label>
    </div>
  )
}
