export interface PropertyRoomTypeDto {
  propertyId: number
  roomTypeId: number
  roomTypeName: string
  monthlyRent: number
  isActive: boolean
  totalRoomCount: number
  availableRoomCount: number
}

export interface RoomTypeCatalogItem {
  roomTypeId: number
  typeName: string
  price: number
}
